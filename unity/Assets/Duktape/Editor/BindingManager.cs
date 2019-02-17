﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

namespace Duktape
{
    using UnityEngine;
    using UnityEditor;

    public class BindingManager
    {
        private TextGenerator log;
        private HashSet<Type> blacklist;
        private HashSet<Type> whitelist;
        private List<Type> types = new List<Type>();
        private List<string> outputFiles = new List<string>();

        public BindingManager()
        {
            var tab = Prefs.GetPrefs().tab;
            var newline = Prefs.GetPrefs().newline;
            log = new TextGenerator(newline, tab);
            blacklist = new HashSet<Type>(new Type[]
            {
                typeof(AOT.MonoPInvokeCallbackAttribute),
            });
            whitelist = new HashSet<Type>(new Type[]
            {
            });
        }

        public void AddExport(Type type)
        {
            types.Add(type);
        }

        public bool IsExported(Type type)
        {
            return types.Contains(type);
        }

        // 是否在黑名单中屏蔽, 或者已知无需导出的类型
        public bool IsExportingBlocked(Type type)
        {
            if (blacklist.Contains(type))
            {
                return true;
            }
            if (type.IsGenericType)
            {
                return true;
            }
            return false;
        }

        // 是否显式要求导出
        public bool IsExportingExplicit(Type type)
        {
            if (whitelist.Contains(type))
            {
                return true;
            }
            return false;
        }

        // 将类型名转换成简单字符串 (比如用于文件名)
        public string GetFileName(Type type)
        {
            return type.FullName.Replace(".", "_");
        }

        public void Collect()
        {
            Collect(Prefs.GetPrefs().explicitAssemblies, false);
            Collect(Prefs.GetPrefs().implicitAssemblies, true);
        }

        // implicitExport: 默认进行导出(黑名单例外), 否则根据导出标记或手工添加
        public void Collect(List<string> assemblyNames, bool implicitExport)
        {
            foreach (var assemblyName in assemblyNames)
            {
                log.AppendLine("assembly: {0}", assemblyName);
                log.AddTabLevel();
                try
                {
                    var assembly = Assembly.Load(assemblyName);
                    var types = assembly.GetExportedTypes();

                    log.AppendLine("types {0}", types.Length);
                    foreach (var type in types)
                    {
                        if (IsExportingBlocked(type))
                        {
                            log.AppendLine("blocked: {0}", type.FullName);
                            continue;
                        }
                        if (implicitExport || IsExportingExplicit(type))
                        {
                            log.AppendLine("export: {0}", type.FullName);
                            this.AddExport(type);
                        }
                    }
                }
                catch (Exception exception)
                {
                    log.AppendLine(exception.ToString());
                }
                log.DecTabLevel();
            }
        }

        // 清理多余文件
        public void Cleanup()
        {
            log.AppendLine("cleanup");
            log.AddTabLevel();
            var outDir = Prefs.GetPrefs().outDir;
            foreach (var file in Directory.GetFiles(outDir))
            {
                var nfile = file;
                if (file.EndsWith(".meta"))
                {
                    nfile = file.Substring(0, file.Length - 5);
                }
                // Debug.LogFormat("checking file {0}", nfile);
                if (!outputFiles.Contains(nfile))
                {
                    File.Delete(file);
                    log.AppendLine("remove unused file {0}", file);
                }
            }
            log.DecTabLevel();
        }

        public void AddOutputFile(string filename)
        {
            outputFiles.Add(filename);
        }

        public void Generate()
        {
            var cg = new CodeGenerator(this);
            var outDir = Prefs.GetPrefs().outDir;
            var tx = ".txt";
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            foreach (var type in types)
            {
                cg.Generate(type);
                cg.WriteTo(outDir, GetFileName(type), tx);
            }

            var logPath = Prefs.GetPrefs().logPath;
            File.WriteAllText(logPath, log.ToString());
        }
    }
}