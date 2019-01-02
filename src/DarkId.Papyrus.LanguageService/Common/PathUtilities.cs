using System;
using System.IO;
using System.Text;

namespace DarkId.Papyrus.LanguageService.Common
{
    public static class PathUtilities
    {
        private static readonly char SeparatorToReplace = Path.DirectorySeparatorChar == '/' ? '\\' : '/';

        public static string GetPathWithoutExtension(string path)
        {
            return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
        }

        public static string GetCombinedOrRooted(string basePath, string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(basePath, path));
        }

        public static string GetUnrootedFullPath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);

            return fullPath.Substring(root.Length);
        }

        public static string Normalize(string path)
        {
            return path.Replace(SeparatorToReplace, Path.DirectorySeparatorChar);
        }

        public static string ToFilePath(this Uri uri)
        {
            if (uri.LocalPath[0] == '/' && uri.LocalPath[2] == ':')
            {
                return Path.GetFullPath(uri.LocalPath.Substring(1));
            }

            return Path.GetFullPath(uri.LocalPath);
        }

        // Thanks, poizan42! https://stackoverflow.com/a/35734486/146765
        public static Uri ToFileUri(string filePath)
        {
            var uri = new StringBuilder();

            foreach (var v in filePath)
            {
                if ((v >= 'a' && v <= 'z') || (v >= 'A' && v <= 'Z') || (v >= '0' && v <= '9') ||
                    v == '+' || v == '/' || v == ':' || v == '.' || v == '-' || v == '_' || v == '~' ||
                    v > '\xFF')
                {
                    uri.Append(v);
                }
                else if (v == Path.DirectorySeparatorChar || v == Path.AltDirectorySeparatorChar)
                {
                    uri.Append('/');
                }
                else
                {
                    uri.Append(String.Format("%{0:X2}", (int)v));
                }
            }

            if (uri.Length >= 2 && uri[0] == '/' && uri[1] == '/') // UNC path
            {
                uri.Insert(0, "file:");
            }
            else if (uri.Length >= 2 && uri[0] == '/' && uri[1] != '/')
            {
                uri.Insert(0, "file:/");
            }
            else
            {
                uri.Insert(0, "file:///");
            }

            return new Uri(uri.ToString());
        }
    }
}