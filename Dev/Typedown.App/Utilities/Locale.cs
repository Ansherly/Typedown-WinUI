using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.Resources.Core;
using Microsoft.UI.Xaml.Markup;

namespace Typedown.Core.Utilities
{
    public static class Locale
    {
        public enum ResourceSource
        {
            All,
            CommonResources,
            DialogResources,
            SettingsResources,
            Resources
        }

        private static readonly string assemblyName = typeof(Locale).Assembly.GetName().Name;

        // Core was merged into the app assembly and its .resw files kept their
        // Resources\Strings\<lang>\ layout, so resource subtrees now include that prefix.
        private const string ResourceRoot = "Resources/Strings/";

        public static IReadOnlyDictionary<ResourceSource, ResourceMap> ResourcesDictionary = new Dictionary<ResourceSource, ResourceMap>()
        {
            {ResourceSource.CommonResources,  GetSubtree(nameof(ResourceSource.CommonResources))},
            {ResourceSource.DialogResources,  GetSubtree(nameof(ResourceSource.DialogResources))},
            {ResourceSource.SettingsResources,  GetSubtree(nameof(ResourceSource.SettingsResources))},
            {ResourceSource.Resources,  GetSubtree(nameof(ResourceSource.Resources))}
        };

        // MRT registers the .resw files as flat subtrees keyed by their bare file name
        // (e.g. "CommonResources/Add"), regardless of the Resources\Strings\<lang>\ folder.
        private static ResourceMap GetSubtree(string name)
        {
            try
            {
                return ResourceManager.Current.MainResourceMap.GetSubtree(name);
            }
            catch
            {
                return null;
            }
        }

        public static Dictionary<string, string> SupportedLangs { get; } = new()
        {
            {"af","Afrikaans"},
            {"am","አማርኛ"},
            {"ar","العربية"},
            {"as","অসমীয়া"},
            {"bg","Български"},
            {"bn","বাংলা"},
            {"bs","Bosnian"},
            {"ca","Català"},
            {"cs","Čeština"},
            {"cy","Cymraeg"},
            {"da","Dansk"},
            {"de","Deutsch"},
            {"el","Ελληνικά"},
            {"en","English"},
            {"es","Español"},
            {"et","Eesti"},
            {"eu","Euskara"},
            {"fa","فارسی"},
            {"fi","Suomi"},
            {"fil","Filipino"},
            {"fr","Français"},
            {"ga","Gaeilge"},
            {"gl","Galego"},
            {"gu","ગુજરાતી"},
            {"he","עברית"},
            {"hi","हिन्दी"},
            {"hr","Hrvatski"},
            {"hu","Magyar"},
            {"hy","Հայերեն"},
            {"id","Indonesia"},
            {"is","Íslenska"},
            {"it","Italiano"},
            {"ja","日本語"},
            {"ka","ქართული"},
            {"kk","Қазақ Тілі"},
            {"km","ខ្មែរ"},
            {"kn","ಕನ್ನಡ"},
            {"ko","한국어"},
            {"lo","ລາວ"},
            {"lt","Lietuvių"},
            {"lv","Latviešu"},
            {"mi","Te Reo Māori"},
            {"mk","Македонски"},
            {"ml","മലയാളം"},
            {"mr","मराठी"},
            {"ms","Melayu"},
            {"mt","Malti"},
            {"nb","Norsk Bokmål"},
            {"ne","नेपाली"},
            {"nl","Nederlands"},
            {"or","ଓଡ଼ିଆ"},
            {"pa","ਪੰਜਾਬੀ"},
            {"pl","Polski"},
            {"prs","دری"},
            {"pt","Português (Brasil)"},
            {"ro","Română"},
            {"ru","Русский"},
            {"sk","Slovenčina"},
            {"sl","Slovenščina"},
            {"sq","Shqip"},
            {"sr-Latn","Srpski (latinica)"},
            {"sv","Svenska"},
            {"sw","Kiswahili"},
            {"ta","தமிழ்"},
            {"te","తెలుగు"},
            {"th","ไทย"},
            {"ti","ትግር"},
            {"tr","Türkçe"},
            {"uk","Українська"},
            {"ur","اردو"},
            {"uz","Uzbek (Latin)"},
            {"vi","Tiếng Việt"},
            {"zh-Hans","中文 (简体)"},
            {"zh-Hant","繁體中文 (繁體)"},
            {"zu","Isi-Zulu"},
        };

        public static Dictionary<string, string> LangsOptions { get; } = new(SupportedLangs.Append(new("default", GetString("UseSystemSetting"))));

        public static string GetLangOptionDisplayName(string key) => LangsOptions[key];

        public static ResourceContext ResourceContext { get; } = new();

        public static string GetString(string key, ResourceSource source = 0)
        {
            try
            {
                key = key?.Replace('.', '/');
                if (source == 0 || !ResourcesDictionary.ContainsKey(source))
                    return ResourcesDictionary.Values
                        .Where(x => x != null)
                        .Select(x => x.GetValue(key)?.ValueAsString)
                        .Where(x => !string.IsNullOrEmpty(x))
                        .FirstOrDefault();

                return ResourcesDictionary[source]?.GetValue(key, ResourceContext)?.ValueAsString;
            }
            catch
            {
                return null;
            }
        }

        public static string GetDialogString(string key)
        {
            return GetString(key, ResourceSource.DialogResources);
        }

        public static string GetTypeString(Type type)
        {
            return (type.GetCustomAttribute(typeof(LocaleAttribute)) as LocaleAttribute)?.Text;
        }
    }

    public class LocaleAttribute : Attribute
    {
        public string[] Keys { get; }

        public string Text => Texts.FirstOrDefault();

        public IEnumerable<string> Texts => Keys.Select(x => Locale.GetString(x));

        public LocaleAttribute(params string[] keys)
        {
            Keys = keys;
        }
    }

    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public class LocaleString : MarkupExtension
    {
        public string Key { get; set; }

        public Locale.ResourceSource Source { get; set; }

        protected override object ProvideValue()
        {
            // WinUI 3 throws "Markup extension could not provide value" on a null return,
            // so never hand back null for an unresolved key.
            return Locale.GetString(Key, Source) ?? Key ?? string.Empty;
        }
    }
}
