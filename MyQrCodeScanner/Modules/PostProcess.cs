using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace MyQrCodeScanner.Modules
{
    /// <summary>
    /// The four entry points of the application.
    /// </summary>
    public enum ScanMode
    {
        Screenshot = 0,
        Camera = 1,
        ImageFile = 2,
        ScannerGun = 3
    }

    /// <summary>
    /// One entry in the global, ordered post-processing rule list. The pattern
    /// is stored without the fixed leading slash and trailing "/g".
    /// </summary>
    public sealed class PostProcessRule : INotifyPropertyChanged
    {
        private int number;
        private string name = string.Empty;
        private string pattern = string.Empty;
        private string matchIndex = string.Empty;
        private string groupIndex = "0";

        public int Number
        {
            get { return number; }
            internal set
            {
                number = value;
                OnPropertyChanged("Number");
                OnPropertyChanged("DisplayName");
            }
        }

        public string DisplayName
        {
            get
            {
                string format = LangHelper.GetStr("PostProcessRuleName");
                if (string.IsNullOrEmpty(format))
                    format = "Rule {0}";
                return string.Format(format, number);
            }
        }

        /// <summary>
        /// Optional user description for this rule. It is only a label and
        /// does not participate in matching.
        /// </summary>
        public string Name
        {
            get { return name; }
            set
            {
                string newValue = value ?? string.Empty;
                if (name == newValue) return;
                name = newValue;
                OnPropertyChanged();
            }
        }

        public string Pattern
        {
            get { return pattern; }
            set
            {
                string newValue = value ?? string.Empty;
                if (pattern == newValue) return;
                pattern = newValue;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 1-based match number. Empty (or 0) means "use the first match",
        /// which is the equivalent of RegExp.exec without a match index.
        /// </summary>
        public string MatchIndex
        {
            get { return matchIndex; }
            set
            {
                string newValue = value ?? string.Empty;
                if (matchIndex == newValue) return;
                matchIndex = newValue;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 0 means the whole match, 1..N selects a capture group.
        /// </summary>
        public string GroupIndex
        {
            get { return groupIndex; }
            set
            {
                string newValue = value ?? string.Empty;
                if (groupIndex == newValue) return;
                groupIndex = newValue;
                OnPropertyChanged();
            }
        }

        public void RefreshDisplayName()
        {
            OnPropertyChanged("DisplayName");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// A scan mode that can opt in to global post-processing.
    /// </summary>
    public sealed class PostProcessModeOption : INotifyPropertyChanged
    {
        private bool enabled;

        public PostProcessModeOption(ScanMode mode)
        {
            Mode = mode;
        }

        public ScanMode Mode { get; private set; }

        public string ModeName
        {
            get
            {
                string name = LangHelper.GetStr("PostProcessMode" + Mode);
                return string.IsNullOrEmpty(name) ? Mode.ToString() : name;
            }
        }

        public bool Enabled
        {
            get { return enabled; }
            set
            {
                if (enabled == value) return;
                enabled = value;
                OnPropertyChanged();
            }
        }

        public void RefreshModeName()
        {
            OnPropertyChanged("ModeName");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Result of applying the ordered rule list. Matched is true only when at
    /// least one rule actually matched and selected a valid group.
    /// </summary>
    public sealed class PostProcessResult
    {
        public PostProcessResult(string text, bool matched)
        {
            Text = text;
            Matched = matched;
        }

        public string Text { get; private set; }
        public bool Matched { get; private set; }
    }

    /// <summary>
    /// Global post-processing rules shared by all selected scan modes.
    /// </summary>
    public static class PostProcessSettings
    {
        private const string SectionName = "postprocess";
        private const string VersionKey = "version";
        private const string VersionValue = "2";
        private const int MaxRules = 100;
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
        private static readonly object SyncRoot = new object();

        private static readonly ObservableCollection<PostProcessRule> rules =
            new ObservableCollection<PostProcessRule>();

        private static readonly PostProcessModeOption[] modeOptions = new[]
        {
            new PostProcessModeOption(ScanMode.Screenshot),
            new PostProcessModeOption(ScanMode.Camera),
            new PostProcessModeOption(ScanMode.ImageFile),
            new PostProcessModeOption(ScanMode.ScannerGun)
        };

        public static ObservableCollection<PostProcessRule> Rules
        {
            get { return rules; }
        }

        public static IReadOnlyList<PostProcessModeOption> ModeOptions
        {
            get { return modeOptions; }
        }

        public static PostProcessModeOption GetModeOption(ScanMode mode)
        {
            foreach (PostProcessModeOption option in modeOptions)
                if (option.Mode == mode)
                    return option;
            return null;
        }

        public static bool IsModeEnabled(ScanMode mode)
        {
            PostProcessModeOption option = GetModeOption(mode);
            return option != null && option.Enabled;
        }

        public static PostProcessRule AddRule()
        {
            lock (SyncRoot)
            {
                PostProcessRule rule = new PostProcessRule();
                rules.Add(rule);
                RefreshRuleNumbersLocked();
                return rule;
            }
        }

        public static void RemoveRule(PostProcessRule rule)
        {
            if (rule == null)
                return;

            lock (SyncRoot)
            {
                rules.Remove(rule);
                RefreshRuleNumbersLocked();
            }
        }

        public static void Load()
        {
            Load(IniHelper.inipath);
        }

        public static void Load(string iniPath)
        {
            string version = IniHelper.GetKeyValue(SectionName, VersionKey, string.Empty, iniPath);
            if (version == VersionValue)
                LoadNewFormat(iniPath);
            else
                MigrateOldFormat(iniPath);
        }

        private static void LoadNewFormat(string iniPath)
        {
            foreach (PostProcessModeOption option in modeOptions)
                option.Enabled = Convert.ToBoolean(IniHelper.GetKeyValue(SectionName, ModuleKey(option.Mode), "false", iniPath));

            string countText = IniHelper.GetKeyValue(SectionName, "rules", "1", iniPath);
            int count;
            if (!int.TryParse(countText, out count) || count < 0)
                count = 1;
            if (count > MaxRules)
                count = MaxRules;

            lock (SyncRoot)
            {
                rules.Clear();
                for (int i = 0; i < count; i++)
                {
                    AddRuleCore(
                        IniHelper.GetKeyValue(SectionName, RuleKey(i, "pattern"), string.Empty, iniPath),
                        IniHelper.GetKeyValue(SectionName, RuleKey(i, "match"), string.Empty, iniPath),
                        IniHelper.GetKeyValue(SectionName, RuleKey(i, "group"), "0", iniPath),
                        IniHelper.GetKeyValue(SectionName, RuleKey(i, "name"), string.Empty, iniPath));
                }
                RefreshRuleNumbersLocked();
            }
        }

        /// <summary>
        /// Keep existing users' per-mode rules when upgrading from the first
        /// fork version (version 1). If several modes were enabled, their
        /// distinct rules become entries in the new global ordered list.
        /// </summary>
        private static void MigrateOldFormat(string iniPath)
        {
            List<PostProcessRule> migrated = new List<PostProcessRule>();
            HashSet<string> seen = new HashSet<string>();

            foreach (PostProcessModeOption option in modeOptions)
            {
                option.Enabled = false;
                string prefix = ModeKey(option.Mode);
                bool oldEnabled = Convert.ToBoolean(IniHelper.GetKeyValue(SectionName, prefix + "_enabled", "false", iniPath));
                string pattern = IniHelper.GetKeyValue(SectionName, prefix + "_pattern", string.Empty, iniPath);
                if (!oldEnabled || string.IsNullOrWhiteSpace(pattern))
                    continue;

                string match = IniHelper.GetKeyValue(SectionName, prefix + "_match", string.Empty, iniPath);
                string group = IniHelper.GetKeyValue(SectionName, prefix + "_group", "0", iniPath);
                string key = pattern + "\u0001" + match + "\u0001" + group;
                if (!seen.Add(key))
                    continue;

                option.Enabled = true;
                PostProcessRule rule = new PostProcessRule();
                rule.Pattern = pattern;
                rule.MatchIndex = match;
                rule.GroupIndex = group;
                migrated.Add(rule);
            }

            lock (SyncRoot)
            {
                rules.Clear();
                foreach (PostProcessRule rule in migrated)
                    rules.Add(rule);
                if (rules.Count == 0)
                    rules.Add(new PostProcessRule());
                RefreshRuleNumbersLocked();
            }
        }

        public static void Save()
        {
            Save(IniHelper.inipath);
        }

        public static void Save(string iniPath)
        {
            IniHelper.SetKeyValue(SectionName, VersionKey, VersionValue, iniPath);
            foreach (PostProcessModeOption option in modeOptions)
                IniHelper.SetKeyValue(SectionName, ModuleKey(option.Mode), option.Enabled.ToString(), iniPath);

            PostProcessRule[] snapshot;
            lock (SyncRoot)
            {
                snapshot = rules.ToArray();
            }

            IniHelper.SetKeyValue(SectionName, "rules", snapshot.Length.ToString(), iniPath);
            for (int i = 0; i < snapshot.Length; i++)
            {
                IniHelper.SetKeyValue(SectionName, RuleKey(i, "name"), snapshot[i].Name ?? string.Empty, iniPath);
                IniHelper.SetKeyValue(SectionName, RuleKey(i, "pattern"), snapshot[i].Pattern ?? string.Empty, iniPath);
                IniHelper.SetKeyValue(SectionName, RuleKey(i, "match"), snapshot[i].MatchIndex ?? string.Empty, iniPath);
                IniHelper.SetKeyValue(SectionName, RuleKey(i, "group"), snapshot[i].GroupIndex ?? "0", iniPath);
            }
        }

        public static void RefreshLanguage()
        {
            foreach (PostProcessModeOption option in modeOptions)
                option.RefreshModeName();
            foreach (PostProcessRule rule in rules)
                rule.RefreshDisplayName();
        }

        /// <summary>
        /// Tries the rules in order against the original scan text. The first
        /// rule that matches wins and all later rules are skipped. If every
        /// rule fails, the original text is returned with Matched=false.
        /// </summary>
        public static PostProcessResult Process(string input, ScanMode mode)
        {
            if (string.IsNullOrEmpty(input) || !IsModeEnabled(mode))
                return new PostProcessResult(input, false);

            PostProcessRule[] snapshot;
            lock (SyncRoot)
            {
                snapshot = rules.ToArray();
            }

            foreach (PostProcessRule rule in snapshot)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Pattern))
                    continue;

                PostProcessResult result = ProcessOne(input, rule);
                if (result.Matched)
                    return result;

                // This rule did not match. The next rule is tried against the
                // original scan text, not against a modified intermediate value.
            }

            return new PostProcessResult(input, false);
        }

        private static PostProcessResult ProcessOne(string input, PostProcessRule rule)
        {
            List<int> matchIndexes;
            List<int> groupIndexes;
            if (!TryParseIndexList(rule.MatchIndex, 0, out matchIndexes) ||
                !TryParseIndexList(rule.GroupIndex, 0, out groupIndexes) ||
                matchIndexes.Count != groupIndexes.Count)
                return new PostProcessResult(input, false);

            try
            {
                Regex regex = new Regex(rule.Pattern, RegexOptions.CultureInvariant, RegexTimeout);
                MatchCollection matches = regex.Matches(input);
                StringBuilder output = new StringBuilder();

                for (int i = 0; i < matchIndexes.Count; i++)
                {
                    int matchIndex = matchIndexes[i];
                    if (matchIndex < 0 || matchIndex >= matches.Count)
                        return new PostProcessResult(input, false);

                    Match match = matches[matchIndex];
                    if (!match.Success)
                        return new PostProcessResult(input, false);

                    int groupIndex = groupIndexes[i];
                    if (groupIndex < 0 || groupIndex >= match.Groups.Count)
                        return new PostProcessResult(input, false);

                    Group group = match.Groups[groupIndex];
                    if (group.Success)
                        output.Append(group.Value);
                }

                return new PostProcessResult(output.ToString(), true);
            }
            catch (RegexMatchTimeoutException)
            {
                return new PostProcessResult(input, false);
            }
            catch (ArgumentException)
            {
                return new PostProcessResult(input, false);
            }
        }

        public static string Apply(string input, ScanMode mode)
        {
            return Process(input, mode).Text;
        }

        /// <summary>
        /// Applies the global rule list to every code in a successful scan.
        /// Returns true if at least one code was actually matched.
        /// </summary>
        public static bool ApplyToResult(MyResult result, ScanMode mode)
        {
            bool anyMatched = false;
            if (result == null || result.status != result_status.ok || result.data == null)
                return false;

            foreach (CodeWithLocation code in result.data)
            {
                PostProcessResult processed = Process(code.data, mode);
                code.data = processed.Text;
                anyMatched |= processed.Matched;
            }
            return anyMatched;
        }

        public static bool TryValidatePattern(string pattern, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(pattern))
                return true;

            try
            {
                new Regex(pattern, RegexOptions.CultureInvariant, RegexTimeout);
                return true;
            }
            catch (ArgumentException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryValidateIndexes(PostProcessRule rule, out string error)
        {
            error = null;
            List<int> matchIndexes;
            List<int> groupIndexes;
            if (!TryParseIndexList(rule.MatchIndex, 0, out matchIndexes) ||
                !TryParseIndexList(rule.GroupIndex, 0, out groupIndexes))
            {
                error = LangHelper.GetStr("PostProcessInvalidIndex");
                return false;
            }
            if (matchIndexes.Count != groupIndexes.Count)
            {
                error = LangHelper.GetStr("PostProcessIndexCountMismatch");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Parses a slash-separated list of non-negative indexes. Empty input
        /// means a single default index, preserving the old single-value form.
        /// </summary>
        public static bool TryParseIndexList(string text, int defaultValue, out List<int> values)
        {
            values = new List<int>();
            if (string.IsNullOrWhiteSpace(text))
            {
                values.Add(defaultValue);
                return true;
            }

            string[] parts = text.Split('/');
            foreach (string part in parts)
            {
                int value;
                if (!int.TryParse(part.Trim(), out value) || value < 0)
                {
                    values.Clear();
                    return false;
                }
                values.Add(value);
            }

            return values.Count > 0;
        }

        private static void AddRuleCore(string pattern, string matchIndex, string groupIndex, string name = "")
        {
            PostProcessRule rule = new PostProcessRule();
            rule.Name = name ?? string.Empty;
            rule.Pattern = pattern ?? string.Empty;
            rule.MatchIndex = matchIndex ?? string.Empty;
            rule.GroupIndex = string.IsNullOrEmpty(groupIndex) ? "0" : groupIndex;
            rules.Add(rule);
        }

        private static void RefreshRuleNumbersLocked()
        {
            for (int i = 0; i < rules.Count; i++)
                rules[i].Number = i + 1;
        }

        private static string ModeKey(ScanMode mode)
        {
            return mode.ToString().ToLowerInvariant();
        }

        private static string ModuleKey(ScanMode mode)
        {
            return "module_" + ModeKey(mode);
        }

        private static string RuleKey(int index, string suffix)
        {
            return "rule" + index + "_" + suffix;
        }
    }
}
