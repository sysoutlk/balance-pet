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
using System.Threading;
using System.Threading.Tasks;

namespace DshPet
{
    internal sealed class DayUsage
    {
        public string Day = "";
        public double Usage;
    }

    internal sealed class Thresholds
    {
        public bool AlertOn;
        public double? AlertBelow;
        public bool BudgetOn;
        public double? Budget;
    }

    internal sealed class Snapshot
    {
        public List<ProviderState> Providers = new List<ProviderState>();
        public List<PlanState> Plans = new List<PlanState>();
        public string PrimaryId = "deepseek";
        public long AtMs;
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
                try
                {
                    Current = Build();
                }
                finally
                {
                    Interlocked.Exchange(ref busy, 0);
                }
                EventHandler handler = Updated;
                if (handler != null) handler(this, EventArgs.Empty);
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
                Task.WaitAll(list.ToArray(), 16000);
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
            Dictionary<string, object> stateRoot = ReadState();
            RollState(stateRoot, TodayKey());
            foreach (ProviderState p in snapshot.Providers)
            {
                if (!p.Enabled || !p.Supported) continue;
                ApplyProviderState(stateRoot, p.Id, p.Balance, null);
                p.TodayUsage = ProviderUsage(stateRoot, p.Id, p.Balance);
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
            snapshot.IsPeak = IsPeak(bj, cfg);
            snapshot.NextChangeAtMs = NextChangeAt(bj, cfg);
            return snapshot;
        }

        private static List<DayUsage> BuildHistory(Dictionary<string, object> stateRoot, ProviderState primary)
        {
            List<DayUsage> history = new List<DayUsage>();
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
            while (history.Count > 6) history.RemoveAt(0);
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
            double below = cfg.GetNumberAt("alert.below", 0);
            limits.AlertOn = cfg.GetFlagAt("alert.balanceWarn", true) && below > 0;
            limits.AlertBelow = below > 0 ? (double?)below : null;
            double budget = cfg.GetNumberAt("alert.budget", 0);
            limits.BudgetOn = cfg.GetFlagAt("alert.budgetWarn", true) && budget > 0;
            limits.Budget = budget > 0 ? (double?)budget : null;
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
                    double debit = Json.ToNumber(Json.Get(node, "debit"), 0);
                    if (debit <= 0) continue;
                    Dictionary<string, object> record = new Dictionary<string, object>();
                    record["day"] = day;
                    record["provider"] = pair.Key;
                    record["usage"] = Math.Round(debit, 8);
                    history.Add(record);
                }
            }
            while (history.Count > 60) history.RemoveAt(0);

            Dictionary<string, object> fresh = new Dictionary<string, object>();
            state["day"] = today;
            state["providers"] = fresh;
            state["history"] = history.ToArray();
        }

        private static void ApplyProviderState(Dictionary<string, object> state, string providerId, double? balance, double? opening)
        {
            Dictionary<string, object> providers = Json.AsObject(Json.Get(state, "providers"));
            Dictionary<string, object> node = Json.AsObject(Json.Get(providers, providerId));
            providers[providerId] = node;
            state["providers"] = providers;

            if (Json.Get(node, "opening") == null && (opening.HasValue || balance.HasValue))
            {
                node["opening"] = opening.HasValue ? (object)opening.Value : (object)balance.Value;
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
            }
            node["updatedAtMs"] = NowMs();
        }

        private static double? ProviderUsage(Dictionary<string, object> state, string providerId, double? balance)
        {
            Dictionary<string, object> node = Json.AsObject(Json.Get(Json.AsObject(Json.Get(state, "providers")), providerId));
            double debit = Json.ToNumber(Json.Get(node, "debit"), 0);
            object openingValue = Json.Get(node, "opening");
            if (openingValue != null && balance.HasValue)
            {
                double opening = Json.ToNumber(openingValue, 0);
                double drop = opening - balance.Value;
                if (drop > debit) debit = drop;
            }
            return Math.Max(0, Math.Round(debit, 8));
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
