using Harmony;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace LangImporter
{
    public class Harmony_Patch
    {
        public static void GenerateError(string message)
        {
            if(path != null) File.AppendAllText(path + "/Error.txt", message + "\n");
        }
        public static Dictionary<string, string> supportedLanguages = new Dictionary<string, string>()
        {
            {"en","English"},
            {"kr","한국어"},
            {"cn","中文(简体)"},
            {"cn_tr","中文(繁體)"},
            {"jp","日本語"},
            {"ru","русский"},
            {"vn","Tiếng Việt"},
            {"bg","български"},
            {"es","Español Latinoamérica"},
            {"fr","français"},
            {"pt_br","Português do Brasil"},
            {"pt_pt","Português"}
        },
        associated_paths = new Dictionary<string, string>()
        {
            // {key}, {path}
        };
        public static List<string> moddedLangs = new List<string>();
        public static string path = null;
        public Harmony_Patch()
        {
            path = Path.GetDirectoryName(Uri.UnescapeDataString(new UriBuilder(Assembly.GetExecutingAssembly().CodeBase).Path));
            if (Directory.Exists(path + "/Languages"))
            {
                Dictionary<string, string> moddedLangs = new Dictionary<string, string>();
                int counter = 1;
                foreach (string dir in Directory.GetDirectories(path + "/Languages"))
                {
                    if (File.Exists(dir+"/LangInfo.xml"))
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(File.ReadAllText(dir + "/LangInfo.xml"));
                        try
                        {
                            string name = doc.SelectSingleNode("langInfo/name").InnerText,
                                key = doc.SelectSingleNode("langInfo/key").InnerText;
                            if (name == "Example") continue;
                            if (supportedLanguages.ContainsKey(key)) key = key + $"modded{counter++}";
                            moddedLangs.Add(key, name);
                            associated_paths.Add(key, dir);
                        }
                        catch { GenerateError($"LangInfo file in {'"'}{dir.Split('\\').Last()}{'"'} folder is broken or doesnt contains necessary information"); }
                    }
                    else
                    {
                        GenerateError($"lack of LangInfo file in {'"'}{dir.Split('\\').Last()}{'"'} folder");
                    }
                }
                if (moddedLangs.Count == 0) return;
                Harmony_Patch.moddedLangs = moddedLangs.Keys.ToList();
                foreach (KeyValuePair<string, string> item in supportedLanguages)
                {
                    try
                    {
                        moddedLangs.Add(item.Key, item.Value);
                    }
                    catch(Exception)
                    {
                        //probably will not get throwed ever
                        GenerateError($"key {'"'}{item.Key}{'"'} is already exist, try to use different one");
                    }
                }
                supportedLanguages = moddedLangs;

                #region patching
                HarmonyInstance harmonyInstance = HarmonyInstance.Create("Lobotomy.justnikocat.LangImporter");
                
                harmonyInstance.Patch(
                    typeof(SupportedLanguage).GetMethod(nameof(SupportedLanguage.GetSupprotedList), AccessTools.all),
                    new HarmonyMethod(typeof(Harmony_Patch).GetMethod("SupportedLanguage_GetSupprotedList")),
                    null,
                null);

                harmonyInstance.Patch(
                    typeof(SupportedLanguage).GetMethod(nameof(SupportedLanguage.GetCurrentLanguageName), AccessTools.all, null, new Type[] { typeof(string) }, null),
                    new HarmonyMethod(typeof(Harmony_Patch).GetMethod("SupportedLanguage_GetCurrentLanguageName")),
                    null,
                null);

                harmonyInstance.Patch(
                    typeof(AssetLoader).GetMethod(nameof(AssetLoader.LoadExternalXML), AccessTools.all),
                    new HarmonyMethod(typeof(Harmony_Patch).GetMethod("AssetLoader_LoadExternalXML")),
                    null,
                null);
                #endregion
            }
            else
            {
                Directory.CreateDirectory(path + "/Language");
            }
        }

        //SupportedLanguage
        public static bool SupportedLanguage_GetSupprotedList(ref List<string> __result)
        {
            __result = supportedLanguages.Keys.ToList<string>();
            return false;
        }
        public static bool SupportedLanguage_GetCurrentLanguageName(ref string __result, string language)
        {
            supportedLanguages.TryGetValue(language, out __result);
            return false;
        }

        //AssetLoader
        public static bool AssetLoader_LoadExternalXML(string src, ref XmlDocument __result)
        {
            if (path == null) return true;
            if (!moddedLangs.Contains(src.Split('_').Last())) return true;
            if (src == "Font") return true;
            string root;
            if (src.Contains("Localize")) root = path + "/" + src.Replace("Localize/", src.Split('_').Last() + "/Localize/") + ".xml";
            else root = path + "/" + src + ".xml";
            if (!File.Exists(root)) return true;
            try
            {
                XmlDocument xml = new XmlDocument();
                xml.LoadXml(File.ReadAllText(root));
                __result = xml;
            }
            catch(Exception)
            {
                __result = null;
            }
            return false;
        }

        //GameStaticDataLoader подгружает инфу об агентах
    }
}
