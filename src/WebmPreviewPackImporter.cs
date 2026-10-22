// Converts a neutral WebM + preview-image package into this app's own pet-pack format.
//
// The source package is never bundled or redistributed. The converter reads the
// user-selected local package, maps its animation pools/actions, and copies or
// transcodes only the media needed by the imported role.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;

namespace BalancePet
{
    internal static class WebmPreviewPackImporter
    {
        private const double GifFps = 12.0;
        private const double DefaultDisplayWidth = 320.0;

        private static readonly Dictionary<string, string> PreviewNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "待机呼吸休闲", "daiji-huxi-xiuxian" },
            { "东张西望", "dongzhangxiwang" },
            { "螃蟹走路", "pangxie-zoulu" },
            { "原地漂浮踏步", "yuandi-piaofu-tabu" },
            { "原地左转奔跑", "yuandi-zuozhuan-benpao" },
            { "悠闲哼歌", "youxian-hengga" },
            { "超大伸懒腰", "chaoda-shenlanyao" },
            { "原地敲击桌面互动", "yuandi-qiaoji-zhuomian-hudong" },
            { "原地重力下蹲压缩", "yuandi-zhongli-xiadun-yasuo" },
            { "哈欠连天", "haqian-liantian" },
            { "原地小憩沉眠", "yuandi-xiaoqi-chenmian" },
            { "女仆屈膝礼仪", "nvpu-quxi-liyi" },
            { "被吓一跳", "beixiayitiao-zhamao" },
            { "小幅度原地360度旋转展示", "xiaofudu-yuandi-360du-xuanzhuan-zhanshi" },
            { "偷吃零食被抓住", "touchi-lingshi-bei-zhuazhu" },
            { "用鲸鱼尾巴拍打地面", "yong-jingyu-weiba-paidadi" },
            { "打瞌睡被惊醒", "da-keshui-bei-jingxing" },
            { "照镜子", "zhao-jingzi" },
            { "整体换装试色", "zhengti-huanzhuang-shise" },
            { "轻快记录", "qingkuai-jilu" },
            { "写代码", "xie-daima" },
            { "摇扇纳凉", "yaoshan-naliang" },
            { "晨间刷牙", "chenjian-shuaya" },
            { "原地专心玩魔方", "yuandi-zhuanxin-wan-mofang" },
            { "原地蹲下玩玩具汽车", "yuandi-dunxia-wan-wanju-qiche" },
            { "鲸鱼吐泡泡特效", "jingyu-tu-paopao-texiao" },
            { "原地跳跃抓碎头顶物品", "yuandi-tiaoyue-zhuasui-touding-wupin" },
            { "玩游戏气急败坏", "wan-youxi-qijibaituai" },
            { "玩水枪", "wan-shuiqiang" },
            { "小提琴演奏", "xiaotiqin-yanzou" },
            { "蓝鲸现世", "lanjing-xianshi" },
            { "优雅女仆舞", "youya-nvpuwu" },
            { "轻快摇摆舞", "qingkuai-yaobaiwu" },
            { "可爱宅舞", "keai-zhaiwu" },
            { "吹气球", "chui-qiqiu" },
            { "动物环绕", "dongwu-huanrao" },
            { "放风筝", "fang-fengzheng" },
            { "拆礼物", "chai-liwu" },
            { "变鸽子", "bian-gezi" },
            { "扑克魔术", "puke-moshu" },
            { "抽陀螺", "chou-tuoluo" },
            { "吹笛子", "chui-dizi" },
            { "蝴蝶蜜蜂环绕头顶开花", "hudie-mifeng-huanrao-touding-kaihua" },
            { "撸猫", "lu-mao" },
            { "凭空生花", "pingkong-shenghua" },
            { "骑木马", "qi-muma" },
            { "三球抛接", "sanqiu-paojie" },
            { "踢毽子", "ti-jianzi" },
            { "下五子棋", "xiawuziqi" },
            { "荡秋千", "dangqiuqian" },
            { "吃白饭", "chi-baifan" },
            { "大口吃零食", "dakou-chi-lingshi" },
            { "吃Token", "chi-token" },
            { "吃早餐", "chi-zaocan" },
            { "吃午餐", "chi-wucan" },
            { "吃晚餐", "chi-wancan" },
            { "吃冰淇淋融化", "chi-bingqilin-ronghua" },
            { "吃大闸蟹", "chi-dazhaxie" },
            { "吃糖葫芦", "chi-tanghulu" },
            { "吃长寿面", "chi-changshoumian" },
            { "吃西瓜", "chi-xigua" },
            { "涮火锅", "shuan-huoguo" },
            { "被落叶淹没", "beiluoye-yanmo" },
            { "中秋赏月吃月饼", "zhongqiu-shangyue-chi-yuebing" },
            { "堆雪人", "duixueren" },
            { "放烟花", "fang-yanhua" },
            { "吃粽子", "chi-zongzi" },
            { "吃年糕", "chi-niangao" },
            { "吃青团", "chi-qingtuan" },
            { "吃腊八粥", "chi-labazhou" },
            { "吃重阳糕", "chi-chongyanggao" },
            { "收红包", "shou-hongbao" },
            { "写福字", "xie-fuzi" },
            { "穿针乞巧", "chuanzhenqiqiao" },
            { "舞狮头", "wu-shitou" },
            { "讨糖南瓜灯", "taotang-nanguadeng" },
            { "插茱萸赏菊", "cha-zhuyu-shangju" },
            { "放河灯", "fanghedeng" },
            { "萌化小幽灵", "menghua-xiaoyouling" },
            { "装点圣诞树", "zhuangdian-shengdanshu" },
            { "放孔明灯", "fang-kongmingdeng" },
            { "吃汤圆", "chitangyuan" },
            { "吃饺子", "chijiaozi" },
            { "是啊，吃什么", "shia-chishenme" },
            { "深度思考碎碎念", "shendu-sikao-suisuinian" },
            { "点击回应-开心跃动", "dianji-huiying-kaixin-yuedong" },
            { "点击回应-害羞惊讶", "dianji-huiying-haixiu-jingya" },
            { "点击回应-傲娇生气", "dianji-huiying-aojiao-shengqi-ceshen-zhanshi" },
            { "点击回应-挠痒咯咯笑", "dianji-huiying-naoyang-gegexiao" },
            { "点击回应-元气挥手", "dianji-huiying-yuanqi-huishou" },
            { "被鼠标拖拽悬空反馈", "beishubiao-tuozhuai-xuankong-fankui" },
            { "余额-钱袋满溢", "qian-dai-man-yi" },
            { "余额-金袋叮当", "jin-dai-ding-dang" },
            { "余额-钱袋如常", "qian-dai-ru-chang" },
            { "余额-数金皱眉", "shu-jin-zhou-mei" },
            { "余额-袋空如洗", "dai-kong-ru-xi" },
            { "余额-分文不剩", "fen-wen-bu-sheng" },
            { "碎碎念-擦桌碎碎念", "suisuinian-cazhuo-suisuinian" },
            { "碎碎念-发呆碎碎念", "suisuinian-fadai-suisuinian" },
            { "碎碎念-对屏碎碎念", "suisuinian-duiping-suisuinian" },
            { "工作状态-思考冒泡", "gongzuozhuangtai-sikao-maopao" },
            { "工作状态-忙碌点按", "gongzuozhuangtai-manglu-dianan" },
            { "工作状态-清点归档", "gongzuozhuangtai-qingdian-guidang" },
            { "工作状态-原地踱步张望", "gongzuozhuangtai-yuandi-duobu-zhangwang" },
            { "工作状态-雀跃庆祝", "gongzuozhuangtai-queyue-qingzhu" },
            { "工作状态-垂头叹气冒汗", "gongzuozhuangtai-chuitou-tanqi-maohan" }
        };

        public static bool TryLocate(string source, out string assetsDir, out string suggestedName)
        {
            assetsDir = "";
            suggestedName = "webm-preview-pack";
            List<string> candidates = new List<string>();
            candidates.Add(Path.Combine(source, "assets"));
            candidates.Add(source);
            foreach (string candidate in candidates)
            {
                string config = Path.Combine(candidate, "config.jsonc");
                string webm = Path.Combine(candidate, "webm");
                string preview = Path.Combine(candidate, "preview");
                if (!File.Exists(config)) continue;
                if (!Directory.Exists(webm) && !Directory.Exists(preview)) continue;
                assetsDir = Path.GetFullPath(candidate);
                return true;
            }
            return false;
        }

        public static bool Import(string assetsDir, string target, out string note, out string error)
        {
            note = "";
            error = "";
            try
            {
                string configPath = Path.Combine(assetsDir, "config.jsonc");
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                object parsed = serializer.DeserializeObject(StripJsonComments(File.ReadAllText(configPath, Encoding.UTF8)));
                Dictionary<string, object> root = parsed as Dictionary<string, object>;
                if (root == null)
                {
                    error = "config.jsonc 无法解析";
                    return false;
                }
                Dictionary<string, object> animations = Json.AsObject(Json.Get(root, "animations"));
                if (animations.Count == 0)
                {
                    error = "config.jsonc 里没有 animations";
                    return false;
                }

                string webmDir = Path.Combine(assetsDir, "webm");
                string previewDir = Path.Combine(assetsDir, "preview");
                string ffmpeg = FindFfmpeg(assetsDir);
                bool usedPreview = false;
                bool usedWebm = false;
                Directory.CreateDirectory(target);
                string clipsDir = Path.Combine(target, "clips");
                Directory.CreateDirectory(clipsDir);

                Dictionary<string, string> clipByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, double> durationByName = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, object> clips = new Dictionary<string, object>();
                int clipIndex = 0;

                List<string> idleNames = Names(Json.Get(animations, "idle"));
                List<string> turnNames = Names(Json.Get(animations, "turn"));
                List<string> dragNames = Names(Json.Get(animations, "drag"));
                List<string> clickNames = Names(Json.Get(animations, "clicks"));
                List<string> moveNames = MoveNames(Json.Get(animations, "moves"));
                List<Dictionary<string, object>> categories = Objects(Json.Get(animations, "categories"));
                Dictionary<string, object> events = Json.AsObject(Json.Get(animations, "events"));

                string idleClip;
                double idleSeconds;
                if (!EnsureClip(idleNames.Count > 0 ? idleNames[0] : "", true, webmDir, previewDir, ffmpeg,
                        clipsDir, clipByName, durationByName, clips, ref clipIndex,
                        ref usedPreview, ref usedWebm, out idleClip, out idleSeconds))
                {
                    error = "找不到 idle 动画素材";
                    return false;
                }
                List<string> idleClips = EnsureClips(idleNames, true, webmDir, previewDir, ffmpeg,
                    clipsDir, clipByName, durationByName, clips, ref clipIndex, ref usedPreview, ref usedWebm);
                if (idleClips.Count == 0) idleClips.Add(idleClip);

                List<string> dragClips = EnsureClips(dragNames, true, webmDir, previewDir, ffmpeg,
                    clipsDir, clipByName, durationByName, clips, ref clipIndex, ref usedPreview, ref usedWebm);
                List<string> clickClips = EnsureClips(clickNames, false, webmDir, previewDir, ffmpeg,
                    clipsDir, clipByName, durationByName, clips, ref clipIndex, ref usedPreview, ref usedWebm);
                List<string> moveClips = EnsureClips(moveNames, true, webmDir, previewDir, ffmpeg,
                    clipsDir, clipByName, durationByName, clips, ref clipIndex, ref usedPreview, ref usedWebm);
                List<string> turnClips = EnsureClips(turnNames, false, webmDir, previewDir, ffmpeg,
                    clipsDir, clipByName, durationByName, clips, ref clipIndex, ref usedPreview, ref usedWebm);

                Dictionary<string, object> states = new Dictionary<string, object>();
                states["idle"] = idleClips;
                if (moveClips.Count > 0) states["walk"] = moveClips;
                if (dragClips.Count > 0) states["drag"] = dragClips;
                if (clickClips.Count > 0) states["click"] = clickClips;

                Dictionary<string, object> actions = new Dictionary<string, object>();
                double minCategoryWeight = 1000;
                foreach (Dictionary<string, object> category in categories)
                {
                    string categoryName = Convert.ToString(Json.Get(category, "id"), CultureInfo.InvariantCulture);
                    double weight = Json.ToNumber(Json.Get(category, "weight"), 10);
                    List<string> entries = Names(Json.Get(category, "actions"));
                    if (entries.Count == 0) continue;
                    double each = weight / entries.Count;
                    if (each < minCategoryWeight) minCategoryWeight = each;
                    foreach (string name in entries)
                    {
                        string clip;
                        double seconds;
                        if (!EnsureClip(name, false, webmDir, previewDir, ffmpeg, clipsDir,
                                clipByName, durationByName, clips, ref clipIndex,
                                ref usedPreview, ref usedWebm, out clip, out seconds)) continue;
                        AddAction(actions, name, name, each, true, "idle", clip, seconds);
                    }
                }
                foreach (string name in moveNames)
                {
                    string clip;
                    double seconds;
                    if (!EnsureClip(name, true, webmDir, previewDir, ffmpeg, clipsDir,
                            clipByName, durationByName, clips, ref clipIndex,
                            ref usedPreview, ref usedWebm, out clip, out seconds)) continue;
                    AddAction(actions, name, name, 5.0 / Math.Max(1, moveNames.Count), false, "move", clip, seconds);
                }
                foreach (string name in turnNames)
                {
                    string clip;
                    double seconds;
                    if (!EnsureClip(name, false, webmDir, previewDir, ffmpeg, clipsDir,
                            clipByName, durationByName, clips, ref clipIndex,
                            ref usedPreview, ref usedWebm, out clip, out seconds)) continue;
                    AddAction(actions, name, name, 0, true, "idle", clip, seconds);
                }
                if (clickClips.Count > 0)
                {
                    AddAction(actions, "click", "点击回应", 0, false, "idle",
                        clickClips[0], AverageDuration(clickClips, durationByName));
                }
                foreach (KeyValuePair<string, object> eventPair in events)
                {
                    foreach (string name in Names(eventPair.Value))
                    {
                        string clip;
                        double seconds;
                        if (!EnsureClip(name, false, webmDir, previewDir, ffmpeg, clipsDir,
                                clipByName, durationByName, clips, ref clipIndex,
                                ref usedPreview, ref usedWebm, out clip, out seconds)) continue;
                        AddAction(actions, name, name, 0, true, "idle", clip, seconds);
                    }
                }

                string packName = "WebM 预览图宠物";
                List<object> pets = Json.AsArray(Json.Get(root, "pets"));
                if (pets.Count > 0)
                {
                    string firstName = Convert.ToString(Json.Get(Json.AsObject(pets[0]), "name"), CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(firstName)) packName = firstName;
                }
                Dictionary<string, object> display = new Dictionary<string, object>();
                display["width"] = DefaultDisplayWidth;
                Dictionary<string, object> manifest = new Dictionary<string, object>();
                manifest["id"] = Path.GetFileName(target.TrimEnd('\\', '/'));
                manifest["name"] = packName + "（兼容导入）";
                manifest["display"] = display;
                manifest["states"] = states;
                manifest["clips"] = clips;
                manifest["actions"] = actions;
                string manifestPath = Path.Combine(target, "pet.json");
                File.WriteAllText(manifestPath, serializer.Serialize(manifest), new UTF8Encoding(false));
                note = usedWebm && !usedPreview ? "完整 WebM 转透明 GIF"
                    : (usedWebm ? "WebM + 预览 GIF" : "透明预览 GIF 12fps");
                File.WriteAllText(Path.Combine(target, "compat-source.txt"),
                    "source=webm-preview-pack" + Environment.NewLine + "mode=" + note + Environment.NewLine,
                    new UTF8Encoding(false));
                Log.Write("webm preview compatible import: " + packName + " mode=" + note);
                return true;
            }
            catch (Exception ex)
            {
                error = "WebM 预览图素材包导入失败：" + ex.Message;
                return false;
            }
        }

        private static List<string> EnsureClips(List<string> names, bool loop, string webmDir, string previewDir,
            string ffmpeg, string clipsDir, Dictionary<string, string> clipByName,
            Dictionary<string, double> durationByName, Dictionary<string, object> clips, ref int clipIndex,
            ref bool usedPreview, ref bool usedWebm)
        {
            List<string> result = new List<string>();
            foreach (string name in names)
            {
                string clip;
                double seconds;
                if (EnsureClip(name, loop, webmDir, previewDir, ffmpeg, clipsDir, clipByName,
                        durationByName, clips, ref clipIndex, ref usedPreview, ref usedWebm,
                        out clip, out seconds))
                {
                    if (!result.Contains(clip)) result.Add(clip);
                }
            }
            return result;
        }

        private static bool EnsureClip(string name, bool loop, string webmDir, string previewDir,
            string ffmpeg, string clipsDir, Dictionary<string, string> clipByName,
            Dictionary<string, double> durationByName, Dictionary<string, object> clips, ref int clipIndex,
            ref bool usedPreview, ref bool usedWebm, out string clipKey, out double seconds)
        {
            clipKey = "";
            seconds = 0;
            if (!IsSafeFileName(name)) return false;
            if (clipByName.TryGetValue(name, out clipKey))
            {
                durationByName.TryGetValue(name, out seconds);
                return true;
            }

            clipKey = "clip-" + clipIndex.ToString("000", CultureInfo.InvariantCulture);
            string output = Path.Combine(clipsDir, "clip-" + clipIndex.ToString("000", CultureInfo.InvariantCulture) + ".gif");
            clipIndex++;
            string webm = Path.Combine(webmDir, name + ".webm");
            bool copied = false;
            if (!string.IsNullOrEmpty(ffmpeg) && File.Exists(webm))
            {
                copied = ConvertWebm(ffmpeg, webm, output);
                if (copied) usedWebm = true;
            }
            if (!copied)
            {
                string preview = FindPreview(previewDir, name);
                if (string.IsNullOrEmpty(preview)) return false;
                File.Copy(preview, output, true);
                usedPreview = true;
            }

            int frameCount = GifFrameCount(output);
            if (frameCount <= 0) return false;
            seconds = Math.Max(0.3, Math.Round(frameCount / GifFps, 3));
            Dictionary<string, object> clip = new Dictionary<string, object>();
            clip["path"] = "clips/" + Path.GetFileName(output);
            clip["fps"] = GifFps;
            clip["loop"] = loop;
            clips[clipKey] = clip;
            clipByName[name] = clipKey;
            durationByName[name] = seconds;
            return true;
        }

        private static bool IsSafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "." || name == "..") return false;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            return string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal);
        }

        private static void AddAction(Dictionary<string, object> actions, string key, string label,
            double weight, bool walking, string category, string clip, double seconds)
        {
            if (actions.ContainsKey(key)) return;
            Dictionary<string, object> action = new Dictionary<string, object>();
            action["name"] = label;
            action["clips"] = new string[] { clip };
            action["seconds"] = seconds;
            action["weight"] = weight;
            action["walking"] = walking;
            action["category"] = category;
            actions[key] = action;
        }

        private static double AverageDuration(List<string> clips, Dictionary<string, double> durations)
        {
            double total = 0;
            foreach (string clip in clips)
            {
                double value;
                if (durations.TryGetValue(clip, out value)) total += value;
            }
            return clips.Count == 0 ? 2.0 : Math.Round(total / clips.Count, 3);
        }

        private static string FindPreview(string previewDir, string name)
        {
            if (!Directory.Exists(previewDir)) return "";
            string slug;
            if (!PreviewNames.TryGetValue(name, out slug)) slug = name;
            string path = Path.Combine(previewDir, slug + ".gif");
            if (File.Exists(path)) return path;
            path = Path.Combine(previewDir, name + ".gif");
            return File.Exists(path) ? path : "";
        }

        private static int GifFrameCount(string path)
        {
            try
            {
                GifBitmapDecoder decoder = new GifBitmapDecoder(new Uri(path),
                    BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                return decoder.Frames.Count;
            }
            catch
            {
                return 0;
            }
        }

        private static bool ConvertWebm(string ffmpeg, string source, string output)
        {
            try
            {
                string filter = "fps=12,scale=320:-1:flags=lanczos,split[s0][s1];"
                    + "[s0]palettegen=stats_mode=diff[p];"
                    + "[s1][p]paletteuse=dither=bayer:diff_mode=rectangle";
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = ffmpeg;
                info.Arguments = "-y -hide_banner -loglevel error -c:v libvpx-vp9 -i "
                    + Quote(source) + " -vf " + Quote(filter) + " -loop 0 " + Quote(output);
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardError = true;
                using (Process process = Process.Start(info))
                {
                    string error = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(45000))
                    {
                        try { process.Kill(); } catch { }
                        return false;
                    }
                    if (process.ExitCode != 0)
                    {
                        Log.Write("webm-preview-pack ffmpeg failed: " + error);
                        return false;
                    }
                }
                return File.Exists(output) && new FileInfo(output).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string FindFfmpeg(string assetsDir)
        {
            string root = Path.GetFullPath(Path.Combine(assetsDir, "..", ".."));
            string tools = Path.Combine(root, ".tools");
            if (Directory.Exists(tools))
            {
                try
                {
                    foreach (string file in Directory.GetFiles(tools, "ffmpeg.exe", SearchOption.AllDirectories))
                    {
                        return file;
                    }
                }
                catch
                {
                }
            }
            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                foreach (string directory in path.Split(';'))
                {
                    try
                    {
                        string candidate = Path.Combine(directory.Trim(), "ffmpeg.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch
                    {
                    }
                }
            }
            return "";
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static List<string> MoveNames(object value)
        {
            List<string> result = new List<string>();
            Dictionary<string, object> moves = Json.AsObject(value);
            foreach (object item in Json.AsArray(Json.Get(moves, "actions")))
            {
                Dictionary<string, object> map = item as Dictionary<string, object>;
                string name = map == null ? Convert.ToString(item, CultureInfo.InvariantCulture)
                    : Convert.ToString(Json.Get(map, "name"), CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(name) && !result.Contains(name)) result.Add(name);
            }
            return result;
        }

        private static List<Dictionary<string, object>> Objects(object value)
        {
            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
            foreach (object item in Json.AsArray(value))
            {
                Dictionary<string, object> map = item as Dictionary<string, object>;
                if (map != null) result.Add(map);
            }
            return result;
        }

        private static List<string> Names(object value)
        {
            List<string> result = new List<string>();
            foreach (object item in Json.AsArray(value))
            {
                Dictionary<string, object> map = item as Dictionary<string, object>;
                string name = map == null ? Convert.ToString(item, CultureInfo.InvariantCulture)
                    : Convert.ToString(Json.Get(map, "name"), CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(name) && !result.Contains(name)) result.Add(name);
            }
            if (value is string)
            {
                string single = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(single)) result.Add(single);
            }
            return result;
        }

        private static string StripJsonComments(string text)
        {
            StringBuilder result = new StringBuilder(text.Length);
            bool inString = false;
            bool escaped = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (inString)
                {
                    result.Append(ch);
                    if (escaped) escaped = false;
                    else if (ch == '\\') escaped = true;
                    else if (ch == '"') inString = false;
                    continue;
                }
                if (ch == '"')
                {
                    inString = true;
                    result.Append(ch);
                    continue;
                }
                if (ch == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    i += 2;
                    while (i < text.Length && text[i] != '\n' && text[i] != '\r') i++;
                    if (i < text.Length) result.Append(text[i]);
                    continue;
                }
                if (ch == '/' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'))
                    {
                        if (text[i] == '\n' || text[i] == '\r') result.Append(text[i]);
                        i++;
                    }
                    i++;
                    continue;
                }
                result.Append(ch);
            }
            return result.ToString();
        }
    }
}
