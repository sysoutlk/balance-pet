// Data layer: balances from the enabled providers plus a local state file that tracks each
// provider's day opening balance so "used today" and the 7-day chart survive restarts.
// Everything runs standalone; no other application on the machine is required.
//
// Peak/off-peak follows the DeepSeek pricing page (Beijing time): peak is Monday to
// Friday, excluding Chinese public holidays, 09:00-12:00 and 14:00-18:00; weekends,
// holidays and weekend make-up workdays are all off-peak. The holiday list can be
// overridden from pet-config.json ("peakHolidays": ["2026-01-01", ...]).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BalancePet
{
    internal sealed class DayUsage
    {
        public string Day = "";
        public double Usage;
    }

    internal sealed class Thresholds
    {
        public bool AlertOn;
        public bool BudgetOn;
        public bool QuotaOn;                 // 套餐额度提醒总开关
        // 按平台分别设：alerts.<providerId>.below / .budget（> 0 才算数）
        public Dictionary<string, double> Below = new Dictionary<string, double>();
        public Dictionary<string, double> Budget = new Dictionary<string, double>();
        // 套餐窗口用量阈值（百分比）：alerts.<planId>.fiveHour / .sevenDay
        public Dictionary<string, double> FiveHour = new Dictionary<string, double>();
        public Dictionary<string, double> SevenDay = new Dictionary<string, double>();
    }

    internal sealed class Snapshot
    {
        public List<ProviderState> Providers = new List<ProviderState>();
        public List<PlanState> Plans = new List<PlanState>();
        public string PrimaryId = "deepseek";
        public long AtMs;
        public bool HasPeak;                 // 主接口的计价分标准/优惠时段（DeepSeek 的错峰优惠）
        public bool IsPeak;
        public long NextChangeAtMs;
        public List<DayUsage> History = new List<DayUsage>();
        public Thresholds Limits;
        public string Error = "";

        /// <summary>The provider shown as the big number: the configured primary when it
        /// answers, otherwise the first provider that did.</summary>
        public ProviderState Primary
        {
            get
            {
                ProviderState preferred = null;
                ProviderState firstOk = null;
                ProviderState firstAny = null;
                foreach (ProviderState p in Providers)
                {
                    if (!p.Enabled || !p.Supported) continue;
                    if (firstAny == null) firstAny = p;
                    if (p.Id == PrimaryId) preferred = p;
                    if (p.Ok && firstOk == null) firstOk = p;
                }
                if (preferred != null && preferred.Ok) return preferred;
                if (firstOk != null) return firstOk;
                if (preferred != null) return preferred;
                return firstAny;
            }
        }

        public bool Ok
        {
            get
            {
                ProviderState p = Primary;
                return p != null && p.Ok;
            }
        }

        public int OkCount
        {
            get
            {
                int count = 0;
                foreach (ProviderState p in Providers)
                {
                    if (p.Enabled && p.Supported && p.Ok) count++;
                }
                return count;
            }
        }
    }

    internal sealed class DataService
    {
        private static readonly int[] PeakStart = new int[] { 9, 14 };
        private static readonly int[] PeakEnd = new int[] { 12, 18 };

        // Chinese public holidays 2026 (used unless pet-config.json overrides peakHolidays).
        private static readonly string[] Holidays2026 = new string[]
        {
            "2026-01-01","2026-01-02","2026-01-03",
            "2026-02-15","2026-02-16","2026-02-17","2026-02-18","2026-02-19",
            "2026-02-20","2026-02-21","2026-02-22","2026-02-23",
            "2026-04-04","2026-04-05","2026-04-06",
            "2026-05-01","2026-05-02","2026-05-03","2026-05-04","2026-05-05",
            "2026-06-19","2026-06-20","2026-06-21",
            "2026-09-25","2026-09-26","2026-09-27",
            "2026-10-01","2026-10-02","2026-10-03","2026-10-04","2026-10-05","2026-10-06","2026-10-07"
        };

        private readonly Config cfg;
        private Timer timer;
        private int busy;

        public Snapshot Current = new Snapshot();
        public event EventHandler Updated;

        public DataService(Config config)
        {
            cfg = config;
        }

        private string StatePath
        {
            get
            {
                string custom = cfg.GetString("", "statePath", "");
                if (!string.IsNullOrEmpty(custom)) return custom;
                return AppPaths.StatePath;
            }
        }

        public void Start()
        {
            double seconds = cfg.GetNumber("", "pollSeconds", 0);
            if (seconds <= 0) seconds = cfg.GetNumber("bubble", "pollSeconds", 60);
            if (seconds < 15) seconds = 15;
            Refresh();
            timer = new Timer(delegate { Refresh(); }, null, (int)(seconds * 1000), (int)(seconds * 1000));
        }

        public void Stop()
        {
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
        }

        public void Refresh()
        {
            if (Interlocked.Exchange(ref busy, 1) == 1) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool succeeded = false;
                try
                {
                    Current = Build();
                    succeeded = true;
                }
                catch (Exception ex)
                {
                    Log.Write("data refresh failed: " + ex.Message);
                }
                finally
                {
                    Interlocked.Exchange(ref busy, 0);
                }
                if (succeeded)
                {
                    EventHandler handler = Updated;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            });
        }

        /// <summary>Synchronous build used by --self-test and the smoke report.</summary>
        public Snapshot BuildOnce()
        {
            return Build();
        }

        // ------------------------------------------------------------ 汇总
        private Snapshot Build()
        {
            Snapshot snapshot = new Snapshot();
            snapshot.AtMs = NowMs();
            snapshot.PrimaryId = cfg.GetString("", "primaryProvider", "deepseek");
            Dictionary<string, object> stateRoot = ReadState();
            string todayKey = TodayKey();
            RollState(stateRoot, todayKey);
            WriteState(stateRoot);

            List<ProviderDef> defs = Providers.Effective(cfg);
            List<ProviderDef> enabled = new List<ProviderDef>();
            Dictionary<string, ProviderState> states = new Dictionary<string, ProviderState>();
            foreach (ProviderDef def in defs)
            {
                bool on = cfg.GetFlagAt("providers." + def.Id + ".on", def.Id == "deepseek");
                if (!on)
                {
                    ProviderState skipped = new ProviderState();
                    skipped.Id = def.Id;
                    skipped.Name = def.Name;
                    skipped.Currency = def.Currency;
                    skipped.Supported = def.HasBalance;
                    skipped.IsCustom = def.IsCustom;
                    states[def.Id] = skipped;
                    continue;
                }
                enabled.Add(def);
            }

            Dictionary<string, Task<ProviderState>> tasks = new Dictionary<string, Task<ProviderState>>();
            foreach (ProviderDef def in enabled)
            {
                if (!def.HasBalance) continue;
                ProviderDef captured = def;
                tasks[def.Id] = Task.Factory.StartNew(delegate { return Providers.Query(cfg, captured); });
            }
            if (tasks.Count > 0)
            {
                List<Task<ProviderState>> list = new List<Task<ProviderState>>();
                foreach (Task<ProviderState> task in tasks.Values) list.Add(task);
                Task.WaitAll(list.ToArray(), 30000);
            }

            foreach (ProviderDef def in enabled)
            {
                ProviderState state;
                Task<ProviderState> task;
                if (tasks.TryGetValue(def.Id, out task))
                {
                    if (task.IsCompleted)
                    {
                        state = task.Result;
                    }
                    else
                    {
                        state = new ProviderState();
                        state.Id = def.Id;
                        state.Name = def.Name;
                        state.Currency = def.Currency;
                        state.Enabled = true;
                        state.Supported = true;
                        state.Error = "查询超时";
                        state.AtMs = NowMs();
                    }
                }
                else
                {
                    state = new ProviderState();
                    state.Id = def.Id;
                    state.Name = def.Name;
                    state.Currency = def.Currency;
                    state.Enabled = true;
                    state.Supported = false;
                    state.IsCustom = def.IsCustom;
                    state.Error = def.Note;
                    state.AtMs = NowMs();
                }
                state.Enabled = true;
                state.IsCustom = def.IsCustom;
                state.Supported = def.HasBalance;

                states[def.Id] = state;
            }
            // Keep the registry order (DeepSeek first) regardless of enabled/disabled.
            foreach (ProviderDef def in defs) snapshot.Providers.Add(states[def.Id]);

            // Per-provider day tracking (opening / last / debit).
            foreach (ProviderState p in snapshot.Providers)
            {
                if (!p.Enabled || !p.Supported) continue;
                ApplyProviderState(stateRoot, p.Id, p.Balance, null);
                p.TodayUsage = ProviderUsage(stateRoot, p.Id, p.Balance);
                p.LocalUsageSinceMs = ProviderUsageSince(stateRoot, p.Id);
                if (p.UsageExact && p.TodayTotalUsage.HasValue) p.TodayUsage = p.TodayTotalUsage;
            }
            WriteState(stateRoot);

            // Coding-plan usage windows (Kimi Code / Codex / GLM / ... ) in parallel.
            List<PlanDef> planDefs = Plans.Catalog();
            List<PlanDef> enabledPlans = new List<PlanDef>();
            foreach (PlanDef def in planDefs)
            {
                if (cfg.GetFlagAt("plans." + def.Id + ".on", false)) enabledPlans.Add(def);
            }
            List<Task<PlanState>> planTasks = new List<Task<PlanState>>();
            foreach (PlanDef def in enabledPlans)
            {
                PlanDef captured = def;
                planTasks.Add(Task.Factory.StartNew(delegate { return Plans.Query(cfg, captured); }));
            }
            if (planTasks.Count > 0)
            {
                Task.WaitAll(planTasks.ToArray(), 48000);
            }
            for (int i = 0; i < enabledPlans.Count; i++)
            {
                PlanState planState;
                Task<PlanState> task = planTasks[i];
                if (task.IsCompleted)
                {
                    planState = task.Result;
                }
                else
                {
                    planState = new PlanState();
                    planState.Id = enabledPlans[i].Id;
                    planState.Name = enabledPlans[i].Name;
                    planState.Enabled = true;
                    planState.Error = "查询超时";
                    planState.AtMs = NowMs();
                }
                planState.Enabled = true;
                snapshot.Plans.Add(planState);
            }

            snapshot.Limits = ReadLimits();
            ProviderState primary = snapshot.Primary;
            if (primary == null)
            {
                snapshot.Error = "没有启用任何余额接口";
            }
            else if (!primary.Ok)
            {
                snapshot.Error = primary.Error;
            }
            snapshot.History = BuildHistory(stateRoot, primary);

            DateTime bj = BeijingNow();
            // 标准/优惠时段的计价是 DeepSeek 的错峰优惠：看当前真正当主力的接口（还没配 Key 时
            // 按配置里的主接口算），换成别家就不再显示这些字。
            string peakId = primary != null ? primary.Id : snapshot.PrimaryId;
            snapshot.HasPeak = Providers.HasPeakPricing(peakId);
            if (snapshot.HasPeak)
            {
                snapshot.IsPeak = IsPeak(bj, cfg);
                snapshot.NextChangeAtMs = NextChangeAt(bj, cfg);
            }
            return snapshot;
        }

        private static List<DayUsage> BuildHistory(Dictionary<string, object> stateRoot, ProviderState primary)
        {
            List<DayUsage> history = new List<DayUsage>();
            if (primary != null && primary.UsageExact && primary.PlatformHistory.Count > 0)
            {
                string todayKey = TodayKey();
                bool hasToday = false;
                foreach (DayUsage item in primary.PlatformHistory)
                {
                    DayUsage copy = new DayUsage();
                    copy.Day = item.Day;
                    copy.Usage = item.Day == todayKey && primary.TodayTotalUsage.HasValue
                        ? primary.TodayTotalUsage.Value : item.Usage;
                    if (copy.Day == todayKey) hasToday = true;
                    history.Add(copy);
                }
                if (!hasToday)
                {
                    DayUsage missingToday = new DayUsage();
                    missingToday.Day = todayKey;
                    missingToday.Usage = primary.TodayTotalUsage.HasValue ? primary.TodayTotalUsage.Value : 0;
                    history.Add(missingToday);
                }
                return history;
            }
            string primaryId = primary == null ? "" : primary.Id;
            foreach (object item in Json.AsArray(Json.Get(stateRoot, "history")))
            {
                Dictionary<string, object> entry = Json.AsObject(item);
                string provider = Convert.ToString(Json.Get(entry, "provider"), CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(provider) && provider != primaryId) continue;
                DayUsage usage = new DayUsage();
                usage.Day = Convert.ToString(Json.Get(entry, "day"), CultureInfo.InvariantCulture);
                usage.Usage = Json.ToNumber(Json.Get(entry, "usage"), 0);
                if (!string.IsNullOrEmpty(usage.Day)) history.Add(usage);
            }
            while (history.Count > 59) history.RemoveAt(0);
            DayUsage today = new DayUsage();
            today.Day = TodayKey();
            today.Usage = primary != null && primary.TodayUsage.HasValue ? primary.TodayUsage.Value : 0;
            history.Add(today);
            return history;
        }

        /// <summary>Alert thresholds come from the pet's own config:
        /// alert.below (balance warning, 0 = off) and alert.budget (daily budget, 0 = off).</summary>
        private Thresholds ReadLimits()
        {
            Thresholds limits = new Thresholds();
            limits.AlertOn = cfg.GetFlagAt("alert.balanceWarn", true);
            limits.BudgetOn = cfg.GetFlagAt("alert.budgetWarn", true);
            limits.QuotaOn = cfg.GetFlagAt("alert.quotaWarn", true);
            // 阈值挂在每个平台自己名下：各家币种/量级差太多，一个全局数字没有意义。
            foreach (ProviderDef def in Providers.Builtin())
            {
                double below = cfg.GetNumberAt("alerts." + def.Id + ".below", 0);
                if (below > 0) limits.Below[def.Id] = below;
                double budget = cfg.GetNumberAt("alerts." + def.Id + ".budget", 0);
                if (budget > 0) limits.Budget[def.Id] = budget;
            }
            // 套餐窗口同理：按套餐分别设「5 小时 ≥ x%」「7 天 ≥ y%」。
            foreach (PlanDef plan in Plans.Catalog())
            {
                double five = cfg.GetNumberAt("alerts." + plan.Id + ".fiveHour", 0);
                if (five > 0) limits.FiveHour[plan.Id] = Math.Min(100, five);
                double seven = cfg.GetNumberAt("alerts." + plan.Id + ".sevenDay", 0);
                if (seven > 0) limits.SevenDay[plan.Id] = Math.Min(100, seven);
            }
            return limits;
        }

        // ------------------------------------------------------------ 本地状态
        private Dictionary<string, object> ReadState()
        {
            return Json.ReadObject(StatePath);
        }

        private void WriteState(Dictionary<string, object> state)
        {
            Json.WriteObject(StatePath, state);
        }

        private static void RollState(Dictionary<string, object> state, string today)
        {
            string day = Convert.ToString(Json.Get(state, "day"), CultureInfo.InvariantCulture);
            if (day == today) return;
            Log.Write("state roll " + (string.IsNullOrEmpty(day) ? "(none)" : day) + " -> " + today);

            Dictionary<string, object> providers = Json.AsObject(Json.Get(state, "providers"));
            if (providers.Count == 0)
            {
                // Migrate the v1 (single-provider) layout: debit lived at the top level.
                Dictionary<string, object> migrated = new Dictionary<string, object>();
                if (Json.Get(state, "debit") != null || Json.Get(state, "opening") != null)
                {
                    migrated["deepseek"] = Json.AsObject(state);
                }
                providers = migrated;
            }

            List<object> history = new List<object>();
            foreach (object item in Json.AsArray(Json.Get(state, "history")))
            {
                Dictionary<string, object> entry = Json.AsObject(item);
                if (Convert.ToString(Json.Get(entry, "day"), CultureInfo.InvariantCulture) == day) continue;
                history.Add(entry);
            }
            if (!string.IsNullOrEmpty(day))
            {
                foreach (KeyValuePair<string, object> pair in providers)
                {
                    Dictionary<string, object> node = Json.AsObject(pair.Value);
                    object lastValue = Json.Get(node, "last");
                    double? last = lastValue == null ? (double?)null : Json.ToNumber(lastValue, 0);
                    double usage = NodeUsage(node, last);
                    if (usage <= 0) continue;
                    Dictionary<string, object> record = new Dictionary<string, object>();
                    record["day"] = day;
                    record["provider"] = pair.Key;
                    record["usage"] = Math.Round(usage, 8);
                    record["opening"] = Json.ToNumber(Json.Get(node, "opening"), 0);
                    record["closing"] = last.HasValue ? (object)last.Value : null;
                    record["credit"] = Json.ToNumber(Json.Get(node, "credit"), 0);
                    history.Add(record);
                }
            }
            while (history.Count > 60) history.RemoveAt(0);

            Dictionary<string, object> fresh = new Dictionary<string, object>();
            bool previousDay = IsPreviousDay(day, today);
            if (previousDay)
            {
                foreach (KeyValuePair<string, object> pair in providers)
                {
                    Dictionary<string, object> oldNode = Json.AsObject(pair.Value);
                    object closingValue = Json.Get(oldNode, "last");
                    if (closingValue == null) continue;
                    double observedAt = Json.ToNumber(
                        Json.Get(oldNode, "balanceAtMs"),
                        Json.ToNumber(Json.Get(oldNode, "updatedAtMs"), 0));
                    if (!ObservationNearMidnight(today, observedAt)) continue;
                    double closing = Json.ToNumber(closingValue, 0);
                    Dictionary<string, object> carried = new Dictionary<string, object>();
                    carried["opening"] = closing;
                    carried["last"] = closing;
                    carried["carriedFrom"] = day;
                    carried["observedFromMs"] = BeijingDayStartMs(today);
                    carried["balanceAtMs"] = observedAt;
                    fresh[pair.Key] = carried;
                }
            }
            state["day"] = today;
            state["providers"] = fresh;
            state["history"] = history.ToArray();
        }

        private static void ApplyProviderState(Dictionary<string, object> state, string providerId, double? balance, double? opening)
        {
            long nowMs = NowMs();
            Dictionary<string, object> providers = Json.AsObject(Json.Get(state, "providers"));
            Dictionary<string, object> node = Json.AsObject(Json.Get(providers, providerId));
            providers[providerId] = node;
            state["providers"] = providers;

            if (Json.Get(node, "opening") == null && (opening.HasValue || balance.HasValue))
            {
                node["opening"] = opening.HasValue ? (object)opening.Value : (object)balance.Value;
                node["observedFromMs"] = nowMs;
            }
            object lastValue = Json.Get(node, "last");
            double? last = lastValue == null ? (double?)null : Json.ToNumber(lastValue, 0);
            if (balance.HasValue)
            {
                if (last.HasValue)
                {
                    if (balance.Value < last.Value)
                    {
                        double debit = Json.ToNumber(Json.Get(node, "debit"), 0) + (last.Value - balance.Value);
                        node["debit"] = Math.Round(debit, 8);
                    }
                    else if (balance.Value > last.Value)
                    {
                        double credit = Json.ToNumber(Json.Get(node, "credit"), 0) + (balance.Value - last.Value);
                        node["credit"] = Math.Round(credit, 8);
                    }
                }
                node["last"] = balance.Value;
                node["balanceAtMs"] = nowMs;
            }
            node["updatedAtMs"] = nowMs;
        }

        private static double? ProviderUsage(Dictionary<string, object> state, string providerId, double? balance)
        {
            Dictionary<string, object> node = Json.AsObject(Json.Get(Json.AsObject(Json.Get(state, "providers")), providerId));
            return NodeUsage(node, balance);
        }

        private static long ProviderUsageSince(Dictionary<string, object> state, string providerId)
        {
            Dictionary<string, object> node = Json.AsObject(Json.Get(Json.AsObject(Json.Get(state, "providers")), providerId));
            object value = Json.Get(node, "observedFromMs");
            if (value == null) return 0;
            try
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static double NodeUsage(Dictionary<string, object> node, double? balance)
        {
            double debit = Json.ToNumber(Json.Get(node, "debit"), 0);
            double credit = Json.ToNumber(Json.Get(node, "credit"), 0);
            object openingValue = Json.Get(node, "opening");
            if (openingValue != null && balance.HasValue)
            {
                double opening = Json.ToNumber(openingValue, 0);
                double net = opening + credit - balance.Value;
                if (net > debit) debit = net;
            }
            return Math.Max(0, Math.Round(debit, 8));
        }

        private static bool IsPreviousDay(string previous, string today)
        {
            if (string.IsNullOrEmpty(previous) || string.IsNullOrEmpty(today)) return false;
            DateTime before;
            DateTime after;
            if (!DateTime.TryParseExact(previous, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out before)) return false;
            if (!DateTime.TryParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out after)) return false;
            return after.Date == before.Date.AddDays(1);
        }

        private static bool ObservationNearMidnight(string today, double observedAtMs)
        {
            long observed = (long)observedAtMs;
            if (observed <= 0) return false;
            long midnight = BeijingDayStartMs(today);
            long gap = midnight - observed;
            return gap >= 0 && gap <= 5 * 60 * 1000L;
        }

        private static long BeijingDayStartMs(string day)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out parsed)) return 0;
            DateTime utc = DateTime.SpecifyKind(parsed.Date.AddHours(-8), DateTimeKind.Utc);
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(utc - epoch).TotalMilliseconds;
        }

        // ------------------------------------------------------------ 峰谷（北京时间 UTC+8）
        public static DateTime BeijingNow()
        {
            return DateTime.UtcNow.AddHours(8);
        }

        public static string TodayKey()
        {
            return BeijingNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string[] Holidays(Config cfg)
        {
            List<string> custom = cfg.GetStrings("", "peakHolidays");
            if (custom.Count > 0) return custom.ToArray();
            return Holidays2026;
        }

        /// <summary>标准计价时段的可读文本（由 PeakStart/PeakEnd 生成，不再两处手写）。</summary>
        public static int PeakWindowCount
        {
            get { return PeakStart.Length; }
        }

        public static int PeakWindowStart(int index)
        {
            return PeakStart[index];
        }

        public static int PeakWindowEnd(int index)
        {
            return PeakEnd[index];
        }

        public static string PeakWindowText()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < PeakStart.Length; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(PeakStart[i].ToString("00", CultureInfo.InvariantCulture));
                sb.Append(":00-");
                sb.Append(PeakEnd[i].ToString("00", CultureInfo.InvariantCulture));
                sb.Append(":00");
            }
            return "工作日 " + sb.ToString();
        }

        public static bool IsPeak(DateTime beijing, Config cfg)
        {
            if (beijing.DayOfWeek == DayOfWeek.Saturday || beijing.DayOfWeek == DayOfWeek.Sunday) return false;
            string key = beijing.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string[] holidays = Holidays(cfg);
            for (int i = 0; i < holidays.Length; i++)
            {
                if (holidays[i] == key) return false;
            }
            for (int i = 0; i < PeakStart.Length; i++)
            {
                if (beijing.Hour >= PeakStart[i] && beijing.Hour < PeakEnd[i]) return true;
            }
            return false;
        }

        public static long NextChangeAt(DateTime beijingNow, Config cfg)
        {
            bool current = IsPeak(beijingNow, cfg);
            DateTime cursor = new DateTime(beijingNow.Year, beijingNow.Month, beijingNow.Day, beijingNow.Hour, beijingNow.Minute, 0, DateTimeKind.Utc).AddMinutes(1);
            for (int step = 0; step < 15 * 24 * 60; step++)
            {
                if (IsPeak(cursor, cfg) != current)
                {
                    long deltaMinutes = (long)(cursor - beijingNow).TotalMinutes;
                    return NowMs() + deltaMinutes * 60000L;
                }
                cursor = cursor.AddMinutes(1);
            }
            return 0;
        }

        public static long NowMs()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }
    }
}
