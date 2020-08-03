namespace DarkId.Papyrus.LanguageService.Projects
{
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "0.0.0.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "PapyrusProject.xsd")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "PapyrusProject.xsd")]
    public class PapyrusProject
    {

        private string[] importsField;

        private FolderType[] foldersField;

        private string[] scriptsField;

        private Variable[] variablesField;

        private string outputField;

        private string flagsField;

        private AsmType asmField;

        private bool asmFieldSpecified;

        private bool optimizeField;

        private bool optimizeFieldSpecified;

        private bool releaseField;

        private bool releaseFieldSpecified;

        private bool finalField;

        private bool finalFieldSpecified;

        /// <remarks/>
        [System.Xml.Serialization.XmlArrayItemAttribute("Import")]
        public string[] Imports
        {
            get
            {
                return this.importsField;
            }
            set
            {
                this.importsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlArrayItemAttribute("Folder")]
        public FolderType[] Folders
        {
            get
            {
                return this.foldersField;
            }
            set
            {
                this.foldersField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlArrayItemAttribute("Script")]
        public string[] Scripts
        {
            get
            {
                return this.scriptsField;
            }
            set
            {
                this.scriptsField = value;
            }
        }

        [System.Xml.Serialization.XmlArrayItemAttribute("Variable")]
        public Variable[] Variables
        {
            get
            {
                return this.variablesField;
            }
            set
            {
                this.variablesField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string Output
        {
            get
            {
                return this.outputField;
            }
            set
            {
                this.outputField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string Flags
        {
            get
            {
                return this.flagsField;
            }
            set
            {
                this.flagsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public AsmType Asm
        {
            get
            {
                return this.asmField;
            }
            set
            {
                this.asmField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool AsmSpecified
        {
            get
            {
                return this.asmFieldSpecified;
            }
            set
            {
                this.asmFieldSpecified = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public bool Optimize
        {
            get
            {
                return this.optimizeField;
            }
            set
            {
                this.optimizeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool OptimizeSpecified
        {
            get
            {
                return this.optimizeFieldSpecified;
            }
            set
            {
                this.optimizeFieldSpecified = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public bool Release
        {
            get
            {
                return this.releaseField;
            }
            set
            {
                this.releaseField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool ReleaseSpecified
        {
            get
            {
                return this.releaseFieldSpecified;
            }
            set
            {
                this.releaseFieldSpecified = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public bool Final
        {
            get
            {
                return this.finalField;
            }
            set
            {
                this.finalField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool FinalSpecified
        {
            get
            {
                return this.finalFieldSpecified;
            }
            set
            {
                this.finalFieldSpecified = value;
            }
        }

        public void ExpandVariables()
        {
            if (Variables?.Length <= 0)
            {
                return;
            }

            foreach (var variable in Variables)
            {
                var name = "@" + variable.Name;
                Output = Output.Replace(name, variable.Value);

                if (Imports?.Length > 0)
                {
                    for (var i = 0; i < Imports.Length; i++)
                    {
                        Imports[i] = Imports[i].Replace(name, variable.Value);
                    }
                }

                if (Scripts?.Length > 0)
                {
                    for (var i = 0; i < Scripts.Length; i++)
                    {
                        Scripts[i] = Scripts[i].Replace(name, variable.Value);
                    }
                }

                if (Folders?.Length > 0)
                {
                    for (var i = 0; i < Folders.Length; i++)
                    {
                        Folders[i].Value = Folders[i].Value.Replace(name, variable.Value);
                    }
                }
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "0.0.0.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "PapyrusProject.xsd")]
    public class FolderType
    {

        private bool noRecurseField;

        private bool noRecurseFieldSpecified;

        private string valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public bool NoRecurse
        {
            get
            {
                return this.noRecurseField;
            }
            set
            {
                this.noRecurseField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool NoRecurseSpecified
        {
            get
            {
                return this.noRecurseFieldSpecified;
            }
            set
            {
                this.noRecurseFieldSpecified = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlTextAttribute()]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "0.0.0.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "PapyrusProject.xsd")]
    public class Variable
    {
        private string nameField;

        private string valueField;

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string Name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "0.0.0.0")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "PapyrusProject.xsd")]
    public enum AsmType
    {

        /// <remarks/>
        None,

        /// <remarks/>
        Keep,

        /// <remarks/>
        Only,

        /// <remarks/>
        Discard
    }
}
