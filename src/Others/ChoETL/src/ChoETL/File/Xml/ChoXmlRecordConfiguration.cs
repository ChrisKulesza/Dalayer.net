﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace ChoETL
{
    [DataContract]
    public class ChoXmlRecordConfiguration : ChoFileRecordConfiguration
    {
        [DataMember]
        public List<ChoXmlRecordFieldConfiguration> XmlRecordFieldConfigurations
        {
            get;
            private set;
        }
        [DataMember]
        public string XPath
        {
            get;
            set;
        }
        [DataMember]
        public int Indent
        {
            get;
            set;
        }
        [DataMember]
        public char IndentChar
        {
            get;
            set;
        }
        public XmlNamespaceManager NamespaceManager
        {
            get;
            set;
        }
        public bool EmitDataType
        {
            get;
            set;
        }
        public XmlSerializer XmlSerializer
        {
            get;
            set;
        }
        [DataMember]
        public bool UseXmlSerialization
        {
            get;
            set;
        }
        [DataMember]
        public ChoNullValueHandling NullValueHandling
        {
            get;
            set;
        }
        [DataMember]
        public string XmlVersion
        { get; set; }
        [DataMember]
        public bool OmitXmlDeclaration { get; set; }
        internal Dictionary<string, ChoXmlRecordFieldConfiguration> RecordFieldConfigurationsDict
        {
            get;
            private set;
        }
        [DataMember]
        public string XmlSchemaNamespace { get; set; }
        [DataMember]
        public string JSONSchemaNamespace { get; set; }
        [DataMember]
        public ChoEmptyXmlNodeValueHandling EmptyXmlNodeValueHandling { get; set; }

        private Func<XElement, XElement> _customNodeSelecter = null;
        public Func<XElement, XElement> CustomNodeSelecter
        {
            get { return _customNodeSelecter; }
            set { if (value == null) return; _customNodeSelecter = value; }
        }

        private bool _ignoreCase = true;
        [DataMember]
        public bool IgnoreCase
        {
            get { return _ignoreCase; }
            set
            {
                _ignoreCase = value;
                StringComparer = StringComparer.Create(Culture == null ? CultureInfo.CurrentCulture : Culture, IgnoreCase);
            }
        }
        [DataMember]
        public string RootName
        {
            get;
            set;
        }
        [DataMember]
        public string NodeName
        {
            get;
            set;
        }
        [DataMember]
        public bool IgnoreNodeName
        {
            get;
            set;
        }
        [DataMember]
        public bool IgnoreRootName
        {
            get;
            set;
        }
        [DataMember]
        public bool RetainAsXmlAwareObjects { get; set; }
        public bool IncludeSchemaInstanceNodes { get; set; }
        internal StringComparer StringComparer
        {
            get;
            private set;
        }
        [DataMember]
        internal bool RetainXmlAttributesAsNative { get; set; }

        private string _defaultNamespacePrefix;
        [DataMember]
        public string DefaultNamespacePrefix
        {
            get { return _defaultNamespacePrefix; }
            set
            {
                if (value.IsNullOrWhiteSpace())
                    return;

                _defaultNamespacePrefix = value.Trim();
            }
        }
        internal XNamespace NS
        {
            get
            {
                var nsm = new ChoXmlNamespaceManager(NamespaceManager);
                return nsm.GetNamespaceForPrefix(DefaultNamespacePrefix);
            }
        }
        internal bool FlatToNestedObjectSupport
        {
            get;
            set;
        }

        public readonly dynamic Context = new ChoDynamicObject();

        internal bool IsComplexXPathUsed = true;
        public ChoXmlRecordFieldConfiguration this[string name]
        {
            get
            {
                return XmlRecordFieldConfigurations.Where(i => i.Name == name).FirstOrDefault();
            }
        }

        public ChoXmlRecordConfiguration() : this(null)
        {

        }

        internal ChoXmlRecordConfiguration(Type recordType) : base(recordType)
        {
            XmlRecordFieldConfigurations = new List<ChoXmlRecordFieldConfiguration>();

            XmlVersion = "1.0";
            OmitXmlDeclaration = true;
            Indent = 2;
            IndentChar = ' ';
            IgnoreCase = true;
            NullValueHandling = ChoNullValueHandling.Empty;
            NamespaceManager = new XmlNamespaceManager(new NameTable());
            if (recordType != null)
            {
                Init(recordType);
            }

            if (XPath.IsNullOrEmpty())
            {
                //XPath = "//*";
            }
        }

        protected override void Clone(ChoRecordConfiguration config)
        {
            base.Clone(config);
            if (!(config is ChoXmlRecordConfiguration))
                return;

            ChoXmlRecordConfiguration xconfig = config as ChoXmlRecordConfiguration;

            xconfig.Indent = Indent;
            xconfig.IndentChar = IndentChar;
            xconfig.NamespaceManager = NamespaceManager;
            xconfig.XmlSerializer = XmlSerializer;
            xconfig.NullValueHandling = NullValueHandling;
            xconfig.IgnoreCase = IgnoreCase;
        }

        public ChoXmlRecordConfiguration Clone(Type recordType = null)
        {
            ChoXmlRecordConfiguration config = new ChoXmlRecordConfiguration(recordType);
            Clone(config);

            return config;
        }

        protected override void Init(Type recordType)
        {
            base.Init(recordType);

            ChoXmlRecordObjectAttribute recObjAttr = ChoType.GetAttribute<ChoXmlRecordObjectAttribute>(recordType);
            if (recObjAttr != null)
            {
            }

            if (XmlRecordFieldConfigurations.Count == 0)
                DiscoverRecordFields(recordType);
        }

        internal void UpdateFieldTypesIfAny(Dictionary<string, Type> dict)
        {
            if (dict == null || RecordFieldConfigurationsDict == null)
                return;

            foreach (var key in dict.Keys)
            {
                if (RecordFieldConfigurationsDict.ContainsKey(key) && dict[key] != null)
                    RecordFieldConfigurationsDict[key].FieldType = dict[key];
            }
        }

        public override void MapRecordFields<T>()
        {
            MapRecordFields(typeof(T));
        }

        public override void MapRecordFields(params Type[] recordTypes)
        {
            if (recordTypes == null)
                return;

            DiscoverRecordFields(recordTypes.FirstOrDefault());
            foreach (var rt in recordTypes.Skip(1))
                DiscoverRecordFields(rt, false);
        }

        private void DiscoverRecordFields(Type recordType, bool clear = true)
        {
            if (clear)
                XmlRecordFieldConfigurations.Clear();
            DiscoverRecordFields(recordType, null,
                ChoTypeDescriptor.GetProperties(recordType).Where(pd => pd.Attributes.OfType<ChoXmlNodeRecordFieldAttribute>().Any()).Any());
        }

        private void DiscoverRecordFields(Type recordType, string declaringMember, bool optIn = false)
        {
            if (!recordType.IsDynamicType())
            {
                IsComplexXPathUsed = false;
                Type pt = null;
                if (optIn) //ChoTypeDescriptor.GetProperties(recordType).Where(pd => pd.Attributes.OfType<ChoXmlNodeRecordFieldAttribute>().Any()).Any())
                {
                    foreach (PropertyDescriptor pd in ChoTypeDescriptor.GetProperties(recordType))
                    {
                        pt = pd.PropertyType.GetUnderlyingType();
                        var fa = pd.Attributes.OfType<ChoXmlNodeRecordFieldAttribute>().FirstOrDefault();
                        bool optIn1 = fa == null || fa.UseXmlSerialization ? optIn : ChoTypeDescriptor.GetProperties(pt).Where(pd1 => pd1.Attributes.OfType<ChoXmlNodeRecordFieldAttribute>().Any()).Any();
                        if (false) //optIn1 && !pt.IsSimple() && !typeof(IEnumerable).IsAssignableFrom(pt))
                        {
                            DiscoverRecordFields(pt, declaringMember == null ? pd.Name : "{0}.{1}".FormatString(declaringMember, pd.Name), optIn1);
                        }
                        else if (pd.Attributes.OfType<ChoXmlNodeRecordFieldAttribute>().Any())
                        {
                            bool useCache = true;
                            string xpath = null;
                            ChoXmlNodeRecordFieldAttribute attr = ChoTypeDescriptor.GetPropetyAttribute<ChoXmlNodeRecordFieldAttribute>(pd);
                            if (attr.XPath.IsNullOrEmpty())
                            {
                                if (!attr.FieldName.IsNullOrWhiteSpace())
                                {
                                    attr.XPath = $"/{attr.FieldName}|/@{attr.FieldName}";
                                }
                                else
                                    attr.XPath = xpath = $"/{pd.Name}|/@{pd.Name}";
                                IsComplexXPathUsed = true;
                            }
                            else
                                useCache = false;

                            var obj = new ChoXmlRecordFieldConfiguration(pd.Name, attr, pd.Attributes.OfType<Attribute>().ToArray());
                            obj.FieldType = pt;
                            obj.PropertyDescriptor = pd;
                            obj.DeclaringMember = declaringMember == null ? null : "{0}.{1}".FormatString(declaringMember, pd.Name);
                            obj.UseCache = useCache;
                            if (obj.XPath.IsNullOrWhiteSpace())
                            {
                                if (!obj.FieldName.IsNullOrWhiteSpace())
                                    obj.XPath = $"/{obj.FieldName}|/@{obj.FieldName}";
                                else
                                    obj.XPath = $"/{obj.Name}|/@{obj.Name}";
                            }

                            obj.FieldType = pd.PropertyType.GetUnderlyingType();
                            if (!XmlRecordFieldConfigurations.Any(c => c.Name == pd.Name))
                                XmlRecordFieldConfigurations.Add(obj);
                        }
                    }
                }
                else
                {
                    foreach (PropertyDescriptor pd in ChoTypeDescriptor.GetProperties(recordType))
                    {
                        XmlIgnoreAttribute xiAttr = pd.Attributes.OfType<XmlIgnoreAttribute>().FirstOrDefault();
                        if (xiAttr != null)
                            continue;

                        pt = pd.PropertyType.GetUnderlyingType();
                        if (false) //pt != typeof(object) && !pt.IsSimple() && !typeof(IEnumerable).IsAssignableFrom(pt))
                        {
                            DiscoverRecordFields(pt, declaringMember == null ? pd.Name : "{0}.{1}".FormatString(declaringMember, pd.Name), optIn);
                        }
                        else
                        {
                            var obj = new ChoXmlRecordFieldConfiguration(pd.Name, $"/{pd.Name}|/@{pd.Name}");
                            obj.FieldType = pt;
                            obj.PropertyDescriptor = pd;
                            obj.DeclaringMember = declaringMember == null ? null : "{0}.{1}".FormatString(declaringMember, pd.Name);
                            StringLengthAttribute slAttr = pd.Attributes.OfType<StringLengthAttribute>().FirstOrDefault();
                            if (slAttr != null && slAttr.MaximumLength > 0)
                                obj.Size = slAttr.MaximumLength;

                            XmlElementAttribute xAttr = pd.Attributes.OfType<XmlElementAttribute>().FirstOrDefault();
                            if (xAttr != null && !xAttr.ElementName.IsNullOrWhiteSpace())
                            {
                                obj.FieldName = xAttr.ElementName;
                            }
                            else
                            {
                                XmlAttributeAttribute xaAttr = pd.Attributes.OfType<XmlAttributeAttribute>().FirstOrDefault();
                                if (xAttr != null && !xaAttr.AttributeName.IsNullOrWhiteSpace())
                                {
                                    obj.FieldName = xaAttr.AttributeName;
                                }
                                else
                                {
                                    DisplayNameAttribute dnAttr = pd.Attributes.OfType<DisplayNameAttribute>().FirstOrDefault();
                                    if (dnAttr != null && !dnAttr.DisplayName.IsNullOrWhiteSpace())
                                    {
                                        obj.FieldName = dnAttr.DisplayName.Trim();
                                    }
                                    else
                                    {
                                        DisplayAttribute dpAttr = pd.Attributes.OfType<DisplayAttribute>().FirstOrDefault();
                                        if (dpAttr != null)
                                        {
                                            if (!dpAttr.ShortName.IsNullOrWhiteSpace())
                                                obj.FieldName = dpAttr.ShortName;
                                            else if (!dpAttr.Name.IsNullOrWhiteSpace())
                                                obj.FieldName = dpAttr.Name;
                                        }
                                    }
                                }
                            }
                            DisplayFormatAttribute dfAttr = pd.Attributes.OfType<DisplayFormatAttribute>().FirstOrDefault();
                            if (dfAttr != null && !dfAttr.DataFormatString.IsNullOrWhiteSpace())
                            {
                                obj.FormatText = dfAttr.DataFormatString;
                            }
                            if (dfAttr != null && !dfAttr.NullDisplayText.IsNullOrWhiteSpace())
                            {
                                obj.NullValue = dfAttr.NullDisplayText;
                            }
                            if (!XmlRecordFieldConfigurations.Any(c => c.Name == pd.Name))
                                XmlRecordFieldConfigurations.Add(obj);
                        }
                    }
                }
            }
        }

        public override void Validate(object state)
        {
            base.Validate(state);

            //if (XPath.IsNull())
            //    throw new ChoRecordConfigurationException("XPath can't be null or whitespace.");

            if (XPath.IsNullOrWhiteSpace())
            {
                if (!IsDynamicObject && (RecordType.IsGenericType && RecordType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)))
                {
                    NodeName = NodeName.IsNullOrWhiteSpace() ? "KeyValuePair" : NodeName;
                    RootName = RootName.IsNullOrWhiteSpace() ? "KeyValuePairs" : RootName;
                }
                else if (!IsDynamicObject && !typeof(IChoScalarObject).IsAssignableFrom(RecordType))
                {
                    NodeName = NodeName.IsNullOrWhiteSpace() ? RecordType.Name : NodeName;
                    RootName = RootName.IsNullOrWhiteSpace() ? NodeName.ToPlural() : RootName;
                }
            }
            else
            {
                RootName = RootName.IsNullOrWhiteSpace() ? XPath.SplitNTrim("/").Where(t => !t.IsNullOrWhiteSpace() && t.NTrim() != "." && t.NTrim() != ".." && t.NTrim() != "*").FirstOrDefault() : RootName;
                NodeName = NodeName.IsNullOrWhiteSpace() ? XPath.SplitNTrim("/").Where(t => !t.IsNullOrWhiteSpace() && t.NTrim() != "." && t.NTrim() != ".." && t.NTrim() != "*").Skip(1).FirstOrDefault() : NodeName;
            }

            string rootName = null;
            string nodeName = null;
            ChoXmlDocumentRootAttribute da = TypeDescriptor.GetAttributes(RecordType).OfType<ChoXmlDocumentRootAttribute>().FirstOrDefault();
            if (da != null)
                rootName = da.Name;
            else
            {
                XmlRootAttribute ra = TypeDescriptor.GetAttributes(RecordType).OfType<XmlRootAttribute>().FirstOrDefault();
                if (ra != null)
                    nodeName = ra.ElementName;
            }

            RootName = RootName.IsNullOrWhiteSpace() && !rootName.IsNullOrWhiteSpace() ? rootName : RootName;
            NodeName = NodeName.IsNullOrWhiteSpace() && !nodeName.IsNullOrWhiteSpace() ? nodeName : NodeName;

            RootName = RootName.IsNullOrWhiteSpace() && !NodeName.IsNullOrWhiteSpace() ? NodeName.ToPlural() : RootName;
            if (!RootName.IsNullOrWhiteSpace() && RootName.ToSingular() != RootName)
                NodeName = NodeName.IsNullOrWhiteSpace() && !RootName.IsNullOrWhiteSpace() ? RootName.ToSingular() : NodeName;

            if (RootName.IsNullOrWhiteSpace())
                RootName = "Root";
            if (NodeName.IsNullOrWhiteSpace())
                NodeName = "XElement";

            //Encode Root and node names
            RootName = System.Net.WebUtility.HtmlEncode(RootName);
            NodeName = System.Net.WebUtility.HtmlEncode(NodeName);

            string[] fieldNames = null;
            XElement xpr = null;
            if (state is Tuple<long, XElement>)
                xpr = ((Tuple<long, XElement>)state).Item2;
            else
                fieldNames = state as string[];

            if (AutoDiscoverColumns
                && XmlRecordFieldConfigurations.Count == 0)
            {
                if (RecordType != null && !IsDynamicObject
                    && ChoTypeDescriptor.GetProperties(RecordType).Where(pd => pd.Attributes.OfType<ChoXmlNodeRecordFieldAttribute>().Any()).Any())
                {
                    DiscoverRecordFields(RecordType);
                }
                else if (xpr != null)
                {
                    XmlRecordFieldConfigurations.AddRange(DiscoverRecordFieldsFromXElement(xpr));
                }
                else if (!fieldNames.IsNullOrEmpty())
                {
                    foreach (string fn in fieldNames)
                    {
                        if (IgnoredFields.Contains(fn))
                            continue;

                        if (fn.StartsWith("_"))
                        {
                            string fn1 = fn.Substring(1);
                            var obj = new ChoXmlRecordFieldConfiguration(fn, xPath: $"./{fn1}");
                            obj.FieldName = fn1;
                            obj.IsXmlAttribute = true;
                            XmlRecordFieldConfigurations.Add(obj);
                        }
                        else if (fn.EndsWith("_"))
                        {
                            string fn1 = fn.Substring(0, fn.Length - 1);
                            var obj = new ChoXmlRecordFieldConfiguration(fn, xPath: $"./{fn1}");
                            obj.FieldName = fn1;
                            obj.IsXmlCDATA = true;
                            XmlRecordFieldConfigurations.Add(obj);
                        }
                        else
                        {
                            var obj = new ChoXmlRecordFieldConfiguration(fn, xPath: $"./{fn}");
                            XmlRecordFieldConfigurations.Add(obj);
                        }
                    }
                }
            }
            else
            {
                IsComplexXPathUsed = false;

                foreach (var fc in XmlRecordFieldConfigurations)
                {
                    if (fc.IsArray == null)
                        fc.IsArray = typeof(ICollection).IsAssignableFrom(fc.FieldType);

                    if (fc.FieldName.IsNullOrWhiteSpace())
                        fc.FieldName = fc.Name;

                    if (fc.XPath.IsNullOrWhiteSpace())
                        fc.XPath = $"/{fc.FieldName}|/@{fc.FieldName}";
                    else
                    {
                        if (fc.XPath == fc.FieldName
                            || fc.XPath == $"/{fc.FieldName}" || fc.XPath == $"/{fc.FieldName}" || fc.XPath == $"/{fc.FieldName}"
                            || fc.XPath == $"/@{fc.FieldName}" || fc.XPath == $"/@{fc.FieldName}" || fc.XPath == $"/@{fc.FieldName}"
                            )
                        {

                        }
                        else
                        {
                            IsComplexXPathUsed = true;
                            fc.UseCache = false;
                        }
                    }
                }
            }

            if (XmlRecordFieldConfigurations.Count <= 0)
                throw new ChoRecordConfigurationException("No record fields specified.");

            //Validate each record field
            foreach (var fieldConfig in XmlRecordFieldConfigurations)
                fieldConfig.Validate(this);

            //Check field position for duplicate
            string[] dupFields = XmlRecordFieldConfigurations.GroupBy(i => i.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key).ToArray();

            if (dupFields.Length > 0)
                throw new ChoRecordConfigurationException("Duplicate field(s) [Name(s): {0}] found.".FormatString(String.Join(",", dupFields)));

            PIDict = new Dictionary<string, System.Reflection.PropertyInfo>();
            PDDict = new Dictionary<string, PropertyDescriptor>();
            foreach (var fc in XmlRecordFieldConfigurations)
            {
                if (fc.PropertyDescriptor == null)
                    fc.PropertyDescriptor = ChoTypeDescriptor.GetProperties(RecordType).Where(pd => pd.Name == fc.Name).FirstOrDefault();
                if (fc.PropertyDescriptor == null)
                    continue;

                PIDict.Add(fc.Name, fc.PropertyDescriptor.ComponentType.GetProperty(fc.PropertyDescriptor.Name));
                PDDict.Add(fc.Name, fc.PropertyDescriptor);
            }

            RecordFieldConfigurationsDict = XmlRecordFieldConfigurations.OrderBy(c => c.IsXmlAttribute).Where(i => !i.Name.IsNullOrWhiteSpace()).ToDictionary(i => i.Name);

            if (XmlRecordFieldConfigurations.Where(e => e.IsNullable).Any()
                || NullValueHandling == ChoNullValueHandling.Default)
            {
                if (NamespaceManager != null)
                {
                    if (!NamespaceManager.HasNamespace("xsi"))
                        NamespaceManager.AddNamespace("xsi", ChoXmlSettings.XmlSchemaInstanceNamespace);
                    if (!NamespaceManager.HasNamespace("xsd"))
                        NamespaceManager.AddNamespace("xsd", ChoXmlSettings.XmlSchemaNamespace);
                }
            }

            LoadNCacheMembers(XmlRecordFieldConfigurations);
        }

        internal ChoXmlRecordFieldConfiguration[] DiscoverRecordFieldsFromXElement(XElement xpr)
        {
            IsComplexXPathUsed = false;
            ChoXmlNamespaceManager nsMgr = new ChoXmlNamespaceManager(NamespaceManager);

            Dictionary<string, ChoXmlRecordFieldConfiguration> dict = new Dictionary<string, ChoXmlRecordFieldConfiguration>(StringComparer.CurrentCultureIgnoreCase);
            string name = null;
            foreach (var attr in xpr.Attributes())
            {
                if (!attr.IsValidAttribute(XmlSchemaNamespace, JSONSchemaNamespace, nsMgr, IncludeSchemaInstanceNodes))
                    continue;

                if (!IsInNamespace(xpr.Name, attr.Name))
                    continue;
                //if (!attr.Name.NamespaceName.IsNullOrWhiteSpace()) continue;

                name = GetNameWithNamespace(xpr.Name, attr.Name);

                if (!dict.ContainsKey(name))
                    dict.Add(name, new ChoXmlRecordFieldConfiguration(attr.Name.LocalName, $"/@{name}") { FieldName = name }); // DefaultNamespace.IsNullOrWhiteSpace() ? $"//@{name}" : $"//@{DefaultNamespace}" + ":" + $"{name}") { IsXmlAttribute = true });
                else
                {
                    throw new ChoRecordConfigurationException("Duplicate field(s) [Name(s): {0}] found.".FormatString(name));
                }
            }

            bool hasElements = false;
            //var z = xpr.Elements().ToArray();
            XElement ele = null;
            foreach (var kvp in xpr.Elements().GroupBy(e => e.Name.LocalName).Select(g => new { Name = g.Key, Value = g.ToArray() }))
            {
                if (kvp.Value.Length == 1)
                {
                    ele = kvp.Value.First();
                    if (!IsInNamespace(ele.Name))
                        continue;

                    name = GetNameWithNamespace(ele.Name);

                    hasElements = true;
                    if (!dict.ContainsKey(name))
                        dict.Add(name, new ChoXmlRecordFieldConfiguration(ele.Name.LocalName, $"/{name}") { FieldName = name }); // DefaultNamespace.IsNullOrWhiteSpace() ? $"//{name}" : $"//{DefaultNamespace}" + ":" + $"{name}"));
                    else
                    {
                        if (dict[name].IsXmlAttribute)
                            throw new ChoRecordConfigurationException("Duplicate field(s) [Name(s): {0}] found.".FormatString(name));

                        dict[name].IsArray = true;
                    }
                }
                else if (kvp.Value.Length > 1)
                {
                    ele = kvp.Value.First();
                    if (!IsInNamespace(ele.Name))
                        continue;

                    name = GetNameWithNamespace(ele.Name);

                    hasElements = true;
                    if (!dict.ContainsKey(name))
                        dict.Add(name, new ChoXmlRecordFieldConfiguration(xpr.Name.LocalName, $"/{name}") { FieldName = name }); // DefaultNamespace.IsNullOrWhiteSpace() ? $"//{name}" : $"//{DefaultNamespace}" + ":" + $"{name}"));
                    else
                    {
                        if (dict[name].IsXmlAttribute)
                            throw new ChoRecordConfigurationException("Duplicate field(s) [Name(s): {0}] found.".FormatString(name));

                        dict[name].IsArray = true;
                    }
                }
            }

            //foreach (var ele in xpr.Elements())
            //{
            //    if (!IsInNamespace(ele.Name))
            //        continue;

            //    name = GetNameWithNamespace(ele.Name);

            //    hasElements = true;
            //    if (!dict.ContainsKey(name))
            //        dict.Add(name, new ChoXmlRecordFieldConfiguration(ele.Name.LocalName, $"/{name}") { FieldName = name }); // DefaultNamespace.IsNullOrWhiteSpace() ? $"//{name}" : $"//{DefaultNamespace}" + ":" + $"{name}"));
            //    else
            //    {
            //        if (dict[name].IsXmlAttribute)
            //            throw new ChoRecordConfigurationException("Duplicate field(s) [Name(s): {0}] found.".FormatString(name));

            //        dict[name].IsArray = true;
            //    }
            //}

            if (!hasElements)
            {
                if (IsInNamespace(xpr.Name))
                {
                    //name = xpr.Name.LocalName;
                    name = GetNameWithNamespace(xpr.Name);
                    dict.Add(name, new ChoXmlRecordFieldConfiguration(name, "text()") { FieldName = name });
                }
            }

            return dict.Values.ToArray();
        }

        internal bool IsInNamespace(XName name)
        {
            ChoXmlNamespaceManager nsMgr = new ChoXmlNamespaceManager(NamespaceManager);

            if (name.NamespaceName.IsNullOrWhiteSpace())
                return true;

            if (!name.NamespaceName.IsNullOrWhiteSpace())
            {
                string prefix = nsMgr.GetPrefixOfNamespace(name.NamespaceName);
                if (prefix.IsNullOrWhiteSpace()) return false;

                return true;
            }
            else
                return false;
        }

        internal string GetNameWithNamespace(XName name)
        {
            ChoXmlNamespaceManager nsMgr = new ChoXmlNamespaceManager(NamespaceManager);

            if (!name.NamespaceName.IsNullOrWhiteSpace())
            {
                string prefix = nsMgr.GetPrefixOfNamespace(name.NamespaceName);
                if (prefix.IsNullOrWhiteSpace()) return name.LocalName;

                return prefix + ":" + name.LocalName;
            }
            else
                return name.LocalName;
        }

        internal bool IsInNamespace(XName name, XName propName)
        {
            ChoXmlNamespaceManager nsMgr = new ChoXmlNamespaceManager(NamespaceManager);

            if (propName.NamespaceName.IsNullOrWhiteSpace())
                return true;

            if (!propName.NamespaceName.IsNullOrWhiteSpace())
            {
                string prefix = nsMgr.GetPrefixOfNamespace(propName.NamespaceName);
                if (prefix.IsNullOrWhiteSpace()) return false;

                return true;
            }
            else
                return false;
        }

        internal string GetNameWithNamespace(XName name, XName propName)
        {
            ChoXmlNamespaceManager nsMgr = new ChoXmlNamespaceManager(NamespaceManager);

            if (!propName.NamespaceName.IsNullOrWhiteSpace())
            {
                string prefix = nsMgr.GetPrefixOfNamespace(propName.NamespaceName);
                if (prefix.IsNullOrWhiteSpace()) return propName.LocalName;

                return prefix + ":" + propName.LocalName;
            }
            else
                return propName.LocalName;
        }

        public ChoXmlRecordConfiguration Configure(Action<ChoXmlRecordConfiguration> action)
        {
            if (action != null)
                action(this);

            return this;
        }

        internal void MapRecordField(string fn, Action<ChoXmlRecordFieldConfigurationMap> mapper)
        {
            if (mapper == null)
                return;

            mapper(new ChoXmlRecordFieldConfigurationMap(GetFieldConfiguration(fn)));
        }

        internal ChoXmlRecordFieldConfiguration GetFieldConfiguration(string fn)
        {
            fn = fn.NTrim();
            if (!XmlRecordFieldConfigurations.Any(fc => fc.Name == fn))
                XmlRecordFieldConfigurations.Add(new ChoXmlRecordFieldConfiguration(fn, $"/{fn}"));

            return XmlRecordFieldConfigurations.First(fc => fc.Name == fn);
        }
    }

    public class ChoXmlNamespaceManager
    {
        public readonly IDictionary<string, string> NSDict;

        public ChoXmlNamespaceManager(XmlNamespaceManager nsMgr)
        {
            NSDict = nsMgr.GetNamespacesInScope(XmlNamespaceScope.All);
        }

        public string GetPrefixOfNamespace(string ns)
        {
            return NSDict.Where(Xml => Xml.Value == ns && !Xml.Key.IsNullOrWhiteSpace()).Select(Xml => Xml.Key).FirstOrDefault();
        }

        public string GetNamespaceForPrefix(string prefix)
        {
            if (prefix != null && NSDict.ContainsKey(prefix))
                return NSDict[prefix];
            else
                return null;
        }

        public string GetFirstDefaultNamespace()
        {
            return NSDict.Where(kvp => kvp.Key != "xml").Select(kvp => kvp.Value).FirstOrDefault();
        }
    }
}
