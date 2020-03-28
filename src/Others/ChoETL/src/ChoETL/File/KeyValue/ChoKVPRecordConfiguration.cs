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

namespace ChoETL
{
    [DataContract]
    public class ChoKVPRecordConfiguration : ChoFileRecordConfiguration
    {
        [DataMember]
        public ChoKVPFileHeaderConfiguration FileHeaderConfiguration
        {
            get;
            set;
        }
        [DataMember]
        public string RecordStart
        {
            get;
            set;
        }
        [DataMember]
        public string RecordEnd
        {
            get;
            set;
        }
        [DataMember]
        public string Separator
        {
            get;
            set;
        }
        public char[] LineContinuationChars
        {
            get;
            set;
        }
        public readonly dynamic Context = new ChoDynamicObject();

        [DataMember]
        public List<ChoKVPRecordFieldConfiguration> KVPRecordFieldConfigurations
        {
            get;
            private set;
        }

        internal Dictionary<string, ChoKVPRecordFieldConfiguration> RecordFieldConfigurationsDict
        {
            get;
            private set;
        }
        internal Dictionary<string, ChoKVPRecordFieldConfiguration> RecordFieldConfigurationsDict2
        {
            get;
            private set;
        }
        internal Dictionary<string, string> AlternativeKeys
        {
            get;
            set;
        }

        internal KeyValuePair<string, ChoKVPRecordFieldConfiguration>[] FCArray;
        internal bool AutoDiscoveredColumns = false;
        private bool _isWildcardComparisionOnRecordStart = false;
        private bool _isWildcardComparisionOnRecordEnd = false;
        private ChoWildcard _recordStartWildCard = null;
        private ChoWildcard _recordEndWildCard = null;

        public ChoKVPRecordFieldConfiguration this[string name]
        {
            get
            {
                return KVPRecordFieldConfigurations.Where(i => i.Name == name).FirstOrDefault();
            }
        }

        public ChoKVPRecordConfiguration() : this(null)
        {
        }

        internal ChoKVPRecordConfiguration(Type recordType) : base(recordType)
        {
            KVPRecordFieldConfigurations = new List<ChoKVPRecordFieldConfiguration>();
            LineContinuationChars = new char[] { ' ', '\t' };

            if (recordType != null)
            {
                Init(recordType);
            }

            if (Separator.IsNullOrEmpty())
            {
                if (Separator.IsNullOrWhiteSpace())
                    Separator = ":";
            }

            FileHeaderConfiguration = new ChoKVPFileHeaderConfiguration(recordType, Culture);
        }

        protected override void Init(Type recordType)
        {
            base.Init(recordType);

            ChoKVPRecordObjectAttribute recObjAttr = ChoType.GetAttribute<ChoKVPRecordObjectAttribute>(recordType);
            if (recObjAttr != null)
            {
                Separator = recObjAttr.Separator;
                RecordStart = recObjAttr.RecordStart;
                RecordEnd = recObjAttr.RecordEnd;
                LineContinuationChars = recObjAttr.LineContinuationChars;
            }
            if (IgnoreFieldValueMode == null)
                IgnoreFieldValueMode = ChoIgnoreFieldValueMode.Empty;

            if (KVPRecordFieldConfigurations.Count == 0)
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
                KVPRecordFieldConfigurations.Clear();
            DiscoverRecordFields(recordType, null,
                ChoTypeDescriptor.GetProperties(recordType).Where(pd => pd.Attributes.OfType<ChoKVPRecordFieldAttribute>().Any()).Any());
        }

        private void DiscoverRecordFields(Type recordType, string declaringMember, bool optIn = false)
        {
            if (!recordType.IsDynamicType())
            {
                Type pt = null;
                if (optIn) //ChoTypeDescriptor.GetProperties(recordType).Where(pd => pd.Attributes.OfType<ChoKVPRecordFieldAttribute>().Any()).Any())
                {
                    foreach (PropertyDescriptor pd in ChoTypeDescriptor.GetProperties(recordType))
                    {
                        pt = pd.PropertyType.GetUnderlyingType();
                        if (!pt.IsSimple() && !typeof(IEnumerable).IsAssignableFrom(pt))
                            DiscoverRecordFields(pt, declaringMember == null ? pd.Name : "{0}.{1}".FormatString(declaringMember, pd.Name), optIn);
                        else if (pd.Attributes.OfType<ChoKVPRecordFieldAttribute>().Any())
                        {
                            var obj = new ChoKVPRecordFieldConfiguration(pd.Name, pd.Attributes.OfType<ChoKVPRecordFieldAttribute>().First(), pd.Attributes.OfType<Attribute>().ToArray());
                            obj.FieldType = pt;
                            obj.PropertyDescriptor = pd;
                            obj.DeclaringMember = declaringMember == null ? null : "{0}.{1}".FormatString(declaringMember, pd.Name);
                            if (!KVPRecordFieldConfigurations.Any(c => c.Name == pd.Name))
                                KVPRecordFieldConfigurations.Add(obj);
                        }
                    }
                }
                else
                {
                    foreach (PropertyDescriptor pd in ChoTypeDescriptor.GetProperties(recordType))
                    {
                        pt = pd.PropertyType.GetUnderlyingType();
                        if (pt != typeof(object) && !pt.IsSimple() && !typeof(IEnumerable).IsAssignableFrom(pt))
                            DiscoverRecordFields(pt, declaringMember == null ? pd.Name : "{0}.{1}".FormatString(declaringMember, pd.Name), optIn);
                        else
                        {
                            var obj = new ChoKVPRecordFieldConfiguration(pd.Name);
                            obj.FieldType = pt;
                            obj.PropertyDescriptor = pd;
                            obj.DeclaringMember = declaringMember == null ? null : "{0}.{1}".FormatString(declaringMember, pd.Name);
                            StringLengthAttribute slAttr = pd.Attributes.OfType<StringLengthAttribute>().FirstOrDefault();
                            if (slAttr != null && slAttr.MaximumLength > 0)
                                obj.Size = slAttr.MaximumLength;
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
                            DisplayFormatAttribute dfAttr = pd.Attributes.OfType<DisplayFormatAttribute>().FirstOrDefault();
                            if (dfAttr != null && !dfAttr.DataFormatString.IsNullOrWhiteSpace())
                            {
                                obj.FormatText = dfAttr.DataFormatString;
                            }
                            if (dfAttr != null && !dfAttr.NullDisplayText.IsNullOrWhiteSpace())
                            {
                                obj.NullValue = dfAttr.NullDisplayText;
                            }
                            if (!KVPRecordFieldConfigurations.Any(c => c.Name == pd.Name))
                                KVPRecordFieldConfigurations.Add(obj);
                        }
                    }
                }
            }
        }

        public override void Validate(object state)
        {
            if (state == null)
            {
                base.Validate(state);

                if (Separator.IsNullOrWhiteSpace())
                    throw new ChoRecordConfigurationException("Separator can't be null or whitespace.");
                if (Separator == EOLDelimiter)
                    throw new ChoRecordConfigurationException("Separator [{0}] can't be same as EODDelimiter [{1}]".FormatString(Separator, EOLDelimiter));
                if (Separator.Contains(QuoteChar))
                    throw new ChoRecordConfigurationException("QuoteChar [{0}] can't be one of Delimiter characters [{1}]".FormatString(QuoteChar, Separator));
                if (Comments != null && Comments.Contains(Separator))
                    throw new ChoRecordConfigurationException("One of the Comments contains Delimiter. Not allowed.");
                if (RecordStart.IsNullOrWhiteSpace() && RecordEnd.IsNullOrWhiteSpace())
                {

                }
                else
                {
                    if (RecordStart.IsNullOrWhiteSpace())
                        throw new ChoRecordConfigurationException("RecordStart is missing.");
                    //else if (RecordEnd.IsNullOrWhiteSpace())
                    //    RecordEnd = RecordStart;
                    //throw new ChoRecordConfigurationException("RecordEnd is missing.");

                    if (RecordStart.Contains("*") || RecordStart.Contains("?"))
                    {
                        _isWildcardComparisionOnRecordStart = true;
                        _recordStartWildCard = new ChoWildcard(RecordStart);
                    }
                    if (!RecordEnd.IsNullOrWhiteSpace() && (RecordEnd.EndsWith("*") || RecordStart.Contains("?")))
                    {
                        _isWildcardComparisionOnRecordEnd = true;
                        _recordEndWildCard = new ChoWildcard(RecordEnd);
                    }
                }

                //Validate Header
                if (FileHeaderConfiguration != null)
                {
                    if (FileHeaderConfiguration.FillChar != null)
                    {
                        if (FileHeaderConfiguration.FillChar.Value == ChoCharEx.NUL)
                            throw new ChoRecordConfigurationException("Invalid '{0}' FillChar specified.".FormatString(FileHeaderConfiguration.FillChar));
                        if (Separator.Contains(FileHeaderConfiguration.FillChar.Value))
                            throw new ChoRecordConfigurationException("FillChar [{0}] can't be one of Delimiter characters [{1}]".FormatString(FileHeaderConfiguration.FillChar, Separator));
                        if (EOLDelimiter.Contains(FileHeaderConfiguration.FillChar.Value))
                            throw new ChoRecordConfigurationException("FillChar [{0}] can't be one of EOLDelimiter characters [{1}]".FormatString(FileHeaderConfiguration.FillChar.Value, EOLDelimiter));
                        if ((from comm in Comments
                             where comm.Contains(FileHeaderConfiguration.FillChar.Value.ToString())
                             select comm).Any())
                            throw new ChoRecordConfigurationException("One of the Comments contains FillChar. Not allowed.");
                    }
                }
            }
            else
            {
                string[] headers = state as string[];
                if (AutoDiscoverColumns
                    && KVPRecordFieldConfigurations.Count == 0)
                {
                    AutoDiscoveredColumns = true;
                    if (headers != null && IsDynamicObject)
                    {
                        KVPRecordFieldConfigurations = (from header in headers
                                                        where !IgnoredFields.Contains(header)
                                                        select new ChoKVPRecordFieldConfiguration(header)).ToList();
                    }
                    else
                    {
                        MapRecordFields(RecordType);
                    }
                }

                if (KVPRecordFieldConfigurations.Count <= 0)
                    throw new ChoRecordConfigurationException("No record fields specified.");

                //Validate each record field
                foreach (var fieldConfig in KVPRecordFieldConfigurations)
                    fieldConfig.Validate(this);

                //Check if any field has empty names 
                if (KVPRecordFieldConfigurations.Where(i => i.FieldName.IsNullOrWhiteSpace()).Count() > 0)
                    throw new ChoRecordConfigurationException("Some fields has empty field name specified.");

                //Check field names for duplicate
                string[] dupFields = KVPRecordFieldConfigurations.GroupBy(i => i.FieldName, FileHeaderConfiguration.StringComparer)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key).ToArray();

                if (dupFields.Length > 0 /* && !IgnoreDuplicateFields */)
                    throw new ChoRecordConfigurationException("Duplicate field name(s) [Name: {0}] found.".FormatString(String.Join(",", dupFields)));

                PIDict = new Dictionary<string, System.Reflection.PropertyInfo>();
                PDDict = new Dictionary<string, PropertyDescriptor>();
                foreach (var fc in KVPRecordFieldConfigurations)
                {
                    if (fc.PropertyDescriptor == null)
                        fc.PropertyDescriptor = ChoTypeDescriptor.GetProperties(RecordType).Where(pd => pd.Name == fc.Name).FirstOrDefault();
                    if (fc.PropertyDescriptor == null)
                        continue;

                    PIDict.Add(fc.Name, fc.PropertyDescriptor.ComponentType.GetProperty(fc.PropertyDescriptor.Name));
                    PDDict.Add(fc.Name, fc.PropertyDescriptor);
                }


                RecordFieldConfigurationsDict = KVPRecordFieldConfigurations.Where(i => !i.Name.IsNullOrWhiteSpace()).GroupBy(i => i.Name).Select(g => g.First()).ToDictionary(i => i.Name, FileHeaderConfiguration.StringComparer);
                RecordFieldConfigurationsDict2 = KVPRecordFieldConfigurations.Where(i => !i.FieldName.IsNullOrWhiteSpace()).GroupBy(i => i.Name).Select(g => g.First()).ToDictionary(i => i.FieldName, FileHeaderConfiguration.StringComparer);
                if (IsDynamicObject)
                    AlternativeKeys = RecordFieldConfigurationsDict2.ToDictionary(kvp =>
                    {
                        if (kvp.Key == kvp.Value.Name)
                            return kvp.Value.Name.ToValidVariableName();
                        else
                            return kvp.Value.Name;
                    }, kvp => kvp.Key, FileHeaderConfiguration.StringComparer);
                else
                    AlternativeKeys = RecordFieldConfigurationsDict2.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name, FileHeaderConfiguration.StringComparer);

                FCArray = RecordFieldConfigurationsDict.ToArray();

                LoadNCacheMembers(KVPRecordFieldConfigurations);
            }
        }

        internal bool IsRecordStartMatch(string line)
        {
            if (_isWildcardComparisionOnRecordStart)
            {
                return _recordStartWildCard.IsMatch(line);
            }
            else
            {
                return FileHeaderConfiguration.IsEqual(line, RecordStart);
            }
        }

        internal bool IsRecordEndMatch(string line)
        {
            if (RecordEnd.IsNullOrWhiteSpace())
            {
                return IsRecordStartMatch(line);
            }
            else
            {
                if (_isWildcardComparisionOnRecordEnd)
                {
                    return _recordEndWildCard.IsMatch(line);
                }
                else
                {
                    return FileHeaderConfiguration.IsEqual(line, RecordEnd);
                }
            }
        }

        public ChoKVPRecordConfiguration Configure(Action<ChoKVPRecordConfiguration> action)
        {
            if (action != null)
                action(this);

            return this;
        }
    }
}
