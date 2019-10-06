using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkId.Papyrus.Common;

namespace DarkId.Papyrus.LanguageService.Program
{
    public struct ObjectIdentifier : IEquatable<ObjectIdentifier>
    {
        public const char NamespaceSeparator = ':';
        public const char StructSeparator = '#';

        private static readonly Regex IdentifierRegex = new Regex("((?'namespaces'[\\S\\\\/]+):)?(?'script'[^\\s#]+)((#)(?'struct'\\S+))?");

        public static ObjectIdentifier Parse(string fullyQualifiedName)
        {
            var matches = IdentifierRegex.Match(fullyQualifiedName);

            var scriptName = matches.Groups["script"].Value;
            var structName = matches.Groups["struct"].Value.NullIfWhitespace();

            return new ObjectIdentifier()
            {
                NamespaceParts = matches.Groups["namespaces"].Value.Split(NamespaceSeparator).Where(ns => !string.IsNullOrWhiteSpace(ns)).ToArray(),
                ScriptName = scriptName,
                StructName = structName,
                FullyQualifiedName = fullyQualifiedName,
                FullyQualifiedDisplayName = fullyQualifiedName.Replace(StructSeparator, NamespaceSeparator),
                FullScriptName = (!string.IsNullOrWhiteSpace(matches.Groups["namespaces"].Value) ? matches.Groups["namespaces"].Value + ":" : string.Empty) + scriptName,
                ShortName = scriptName + (!string.IsNullOrWhiteSpace(structName) ? ":" + structName : string.Empty)
            };
        }

        public static ObjectIdentifier FromScriptFilePath(string filePath)
        {
            return Parse(PathUtilities.GetPathWithoutExtension(PathUtilities.Normalize(filePath)).Replace(Path.DirectorySeparatorChar, ':'));
        }

        public static bool TryParse(string fullyQualifiedName, out ObjectIdentifier identifier)
        {
            try
            {
                identifier = Parse(fullyQualifiedName);
                return true;
            }
            catch
            {
                identifier = default;
                return false;
            }
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(ScriptName);

        /// <summary>
        /// An array of the namespace elements of this identifier.
        /// </summary>
        public string[] NamespaceParts { get; private set; }

        /// <summary>
        /// The name of the script of this identifier, excluding namespaces and the struct name.
        /// </summary>
        public string ScriptName { get; private set; }

        /// <summary>
        /// The struct name of this identifier.
        /// </summary>
        public string StructName { get; private set; }

        /// <summary>
        /// The fully qualified name of this identifier including namespaces, the script name and the struct name.
        /// </summary>
        public string FullyQualifiedName { get; private set; }

        /// <summary>
        /// The fully qualified name of this identifier including namespaces, the script name and the struct name,
        /// where the struct delimeter is ':' instead of '#'.
        /// </summary>
        public string FullyQualifiedDisplayName { get; private set; }

        /// <summary>
        /// The script name and namespaces of this identifier.
        /// </summary>
        public string FullScriptName { get; private set; }

        /// <summary>
        /// The script name and struct name of this identifier.
        /// </summary>
        public string ShortName { get; private set; }

        private int _hashCode;

        public string ToScriptFilePath()
        {
            return string.Join(Path.DirectorySeparatorChar.ToString(), NamespaceParts)
                + Path.DirectorySeparatorChar + ScriptName + ".psc";
        }

        public override string ToString()
        {
            return FullyQualifiedName ?? string.Empty;
        }

        public override int GetHashCode()
        {
            // This is lazy, immutable and not a reference type.
            // ReSharper disable NonReadonlyMemberInGetHashCode
            if (_hashCode == 0)
            {
                _hashCode = ToString().ToLower().GetHashCode();
            }

            return _hashCode;
            // ReSharper restore NonReadonlyMemberInGetHashCode
        }

        public bool Equals(ObjectIdentifier other)
        {
            return GetHashCode() == other.GetHashCode();
        }

        public override bool Equals(object other)
        {
            return other is ObjectIdentifier && GetHashCode() == other.GetHashCode();
        }

        public static implicit operator ObjectIdentifier(string id)
        {
            return Parse(id);
        }

        public static implicit operator string(ObjectIdentifier id)
        {
            return id.ToString();
        }

        public static bool operator ==(ObjectIdentifier left, ObjectIdentifier right)
        {
            return left.GetHashCode() == right.GetHashCode();
        }

        public static bool operator !=(ObjectIdentifier left, ObjectIdentifier right)
        {
            return !(left == right);
        }
    }
}