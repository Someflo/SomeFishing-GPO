using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SomeFishingGPO
{
    // Presentation only. Game/OCR strings and state transitions remain unchanged.
    // The table is embedded so selecting a language needs neither files nor network.
    internal static class Localization
    {
        private static readonly Dictionary<string,string> english = Load();
        private static readonly Regex phrases = BuildPattern();
        private static readonly Dictionary<string,string> cache = new Dictionary<string,string>();
        internal static string Language = "es";
        internal static string Normalize(string value) { return value == "en" ? "en" : "es"; }
        internal static IEnumerable<KeyValuePair<string,string>> Entries { get { return english; } }
        private static Dictionary<string,string> Load()
        {
            var result = new Dictionary<string,string>(StringComparer.Ordinal);
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("SomeFishingGPO.Strings.en.tsv"))
            {
                if(stream==null)throw new InvalidOperationException("Missing embedded English translations.");
                using(var reader=new StreamReader(stream,System.Text.Encoding.UTF8))
                {
                    string line;
                    while((line=reader.ReadLine())!=null)
                    {
                        if(line.Length==0||line.StartsWith("#",StringComparison.Ordinal))continue;
                        int separator=line.IndexOf('\t');
                        if(separator<1)throw new InvalidDataException("Invalid translation row.");
                        string source=line.Substring(0,separator).Replace("\\n","\n");
                        string translated=line.Substring(separator+1).Replace("\\n","\n");
                        result.Add(source,translated);
                    }
                }
            }
            return result;
        }
        private static Regex BuildPattern()
        {
            var alternatives=new List<string>();
            foreach(string key in english.Keys.OrderByDescending(k=>k.Length))
                alternatives.Add((char.IsLetterOrDigit(key[0])?@"(?<![\p{L}\p{N}_])":"")+Regex.Escape(key)+
                    (char.IsLetterOrDigit(key[key.Length-1])?@"(?![\p{L}\p{N}_])":""));
            return new Regex(string.Join("|",alternatives.ToArray()),RegexOptions.CultureInvariant|RegexOptions.Compiled);
        }
        internal static string T(string source)
        {
            if(source==null||Language!="en"||source.Length==0)return source;
            // Normalize line endings only for matching; preserve the caller's style.
            string normalized=source.Replace("\r\n","\n"), translated;
            if(!english.TryGetValue(normalized,out translated)&&!cache.TryGetValue(normalized,out translated))
            {
                translated=phrases.Replace(normalized,m=>english[m.Value]);
                if(cache.Count>=512)cache.Clear();
                cache[normalized]=translated;
            }
            return source.IndexOf("\r\n",StringComparison.Ordinal)>=0?translated.Replace("\n","\r\n"):translated;
        }
    }

    // Store the original text separately: language switching never reverse-translates
    // rendered strings or rebuilds controls, so focus, numeric values and selections survive.
    internal sealed class UiTranslations : IDisposable
    {
        private sealed class TextEntry { internal string Source,Rendered; internal bool Writing; }
        private readonly Dictionary<Control,TextEntry> entries=new Dictionary<Control,TextEntry>();
        private readonly List<Control> attached=new List<Control>();
        internal void Attach(Control control)
        {
            if(attached.Contains(control))return;
            attached.Add(control);control.ControlAdded+=OnControlAdded;
            var textBox=control as TextBoxBase;
            if(control is Label||control is ButtonBase||control is Form||(textBox!=null&&textBox.ReadOnly))
            {
                entries.Add(control,new TextEntry{Source=control.Text});control.TextChanged+=OnTextChanged;
                Apply(control,entries[control]);
            }
            foreach(Control child in control.Controls)Attach(child);
        }
        private void OnControlAdded(object sender,ControlEventArgs e) { Attach(e.Control); }
        private void OnTextChanged(object sender,EventArgs e)
        {
            var control=(Control)sender;TextEntry entry=entries[control];
            if(entry.Writing||control.Text==entry.Rendered)return;
            entry.Source=control.Text;Apply(control,entry);
        }
        private static void Apply(Control control,TextEntry entry)
        {
            entry.Rendered=Localization.T(entry.Source);entry.Writing=true;
            try { if(control.Text!=entry.Rendered)control.Text=entry.Rendered; }
            finally { entry.Writing=false; }
        }
        internal string Source(Control control)
        { TextEntry entry;return entries.TryGetValue(control,out entry)?entry.Source:control.Text; }
        internal void Refresh()
        {
            foreach(var pair in entries)if(!pair.Key.IsDisposed)Apply(pair.Key,pair.Value);
            foreach(var control in attached)
            {
                var combo=control as LocalizedComboBox;if(combo!=null&&!combo.IsDisposed)combo.RefreshLanguage();
                if(!control.IsDisposed)control.Invalidate();
            }
        }
        public void Dispose()
        {
            foreach(var control in attached)control.ControlAdded-=OnControlAdded;
            foreach(var control in entries.Keys)control.TextChanged-=OnTextChanged;
            attached.Clear();entries.Clear();
        }
    }
    internal sealed class LocalizedComboBox : ComboBox
    {
        internal LocalizedComboBox()
        { FormattingEnabled=true;Format+=delegate(object sender,ListControlConvertEventArgs e){e.Value=Localization.T(Convert.ToString(e.ListItem));}; }
        internal void RefreshLanguage() { RefreshItems(); }
    }
}
