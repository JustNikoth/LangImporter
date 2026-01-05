using Harmony;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;

namespace LangImporter
{
    public class Harmony_Patch
    {
        //for debugging
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
                #region loading
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
                            if (name == "Example(cant be used as real name)") continue;
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
                if (moddedLangs.Count == 0) { supportedLanguages = null; return; }
                #endregion

                #region marging
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
                #endregion

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
                    new HarmonyMethod(typeof(Harmony_Patch).GetMethod("AssetLoader_LoadExternalXML_Prefix")),
                    new HarmonyMethod(typeof(Harmony_Patch).GetMethod("AssetLoader_LoadExternalXML_Postfix")),
                null);
                #endregion
            }
            else
            {
                supportedLanguages = null;
                Directory.CreateDirectory(path + "/Language");
            }
        }


        #region patches
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
        public static bool AssetLoader_LoadExternalXML_Prefix(string src, ref XmlDocument __result, out bool __state)
        {
            __state = false;
            if (src == "Language/AgentName")
            {
                __state = true;
                return true;
            }
            if (src == "Language/ResearchDesc")
            {

            }
            string key = src.Split('_').Last();
            if (!moddedLangs.Contains(key)) return true;
            string root;
            root = associated_paths[key] + "/Text/" + src + ".xml";
            try
            {
                XmlDocument xml = new XmlDocument();
                xml.LoadXml(File.ReadAllText(root));
                __result = xml;
            }
            catch(Exception e)
            {
                GenerateError("cant load custom lang file: " +e.Message);
                return true;
            }
            return false;
        }

        public void AssetLoader_LoadExternalXML_Postfix(string src, ref XmlDocument __result, bool __state)
        {
            if (__state)
            {
                Dictionary<string, string> reversedSup = supportedLanguages.Keys.ToDictionary((string key) => supportedLanguages[key]);
                if (src == "Language/AgentName")
                {
                    IEnumerator orignames = __result.SelectNodes("root/data").GetEnumerator();
                    foreach (string dir in associated_paths.Values)
                    {
                        try
                        {
                            XmlDocument doc = new XmlDocument();
                            doc.Load(dir + "/LangInfo.xml");
                            string uniqueKey = reversedSup[doc.SelectSingleNode("langInfo/name").InnerText],
                                    key = doc.SelectSingleNode("langInfo/key").InnerText;

                            doc.Load(dir + "/Text/AgentName.xml");
                            foreach(XmlNode curr in doc.SelectNodes("root/data"))
                            {
                                orignames.MoveNext();
                                string localizedName = null;
                                try
                                {
                                    localizedName = curr.Attributes.GetNamedItem(key).InnerText;
                                }
                                catch (Exception) { GenerateError("cant get attr in " + key); }

                                if (localizedName != null)
                                {
                                    if(curr.Attributes.GetNamedItem("id").InnerText != ((XmlNode)orignames.Current).Attributes.GetNamedItem("id").InnerText)
                                        throw new Exception($"localized names have a wrong order or have a lack of all orig names in {uniqueKey} localization");
                                    XmlAttribute att = __result.CreateAttribute(uniqueKey);
                                    att.InnerText = localizedName;
                                    ((XmlNode)orignames.Current).Attributes.Append(att);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            GenerateError(dir + " recieve a error during loading agents: " + e.Message);
                        }
                        orignames.Reset();
                    }
                    
                    IDisposable disposable;
                    if ((disposable = (orignames as IDisposable)) != null)
                    {
                        disposable.Dispose();
                    }
                    
                }
                else if (src == "Language/ResearchDesc")
                {
                    XmlNode origFile = __result.SelectSingleNode("root/supportLanguage");
                    foreach (string mdLn in moddedLangs)
                    {
                        XmlElement ln = __result.CreateElement("ln");
                        ln.InnerText = mdLn;
                        origFile.AppendChild(ln);
                    }

                    IEnumerator orignames = __result.SelectNodes("root/node").GetEnumerator();
                    foreach (string dir in associated_paths.Values)
                    {
                        try
                        {
                            XmlDocument doc = new XmlDocument();
                            doc.Load(dir + "/LangInfo.xml");
                            string uniqueKey = reversedSup[doc.SelectSingleNode("langInfo/name").InnerText],
                                    key = doc.SelectSingleNode("langInfo/key").InnerText;

                            doc.Load(dir + "/Text/ResearchDesc.xml");
                            foreach(XmlNode curr in doc.SelectNodes("root/node"))
                            {
                                if (curr.Attributes.GetNamedItem("id").InnerText != ((XmlNode)orignames.Current).Attributes.GetNamedItem("id").InnerText)
                                    throw new Exception($"localized researches have a wrong order or have a lack of all orig names in {uniqueKey} localization");

                            }
                        }
                        catch (Exception e)
                        {
                            GenerateError(dir + " recieve a error during loading researches: " + e.Message);
                        }
                        orignames.Reset();
                    }
                }
            }
        }

        //GameStaticDataLoader



        #endregion

        //GameStaticDataLoader подгружает инфу об агентах
    }
}
