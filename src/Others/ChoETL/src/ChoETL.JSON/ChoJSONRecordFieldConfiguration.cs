﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace ChoETL
{
    [DataContract]
    public class ChoJSONRecordFieldConfiguration : ChoFileRecordFieldConfiguration
    {
        [DataMember]
        public string JSONPath
        {
            get;
            set;
        }

        public bool IsArray
        {
            get;
            set;
        }
        internal bool ComplexJPathUsed
        {
            get;
            set;
        }

        public bool? UseJSONSerialization
        {
            get;
            set;
        }

        internal string DeclaringMember
        {
            get;
            set;
        }
        internal PropertyDescriptor PropertyDescriptor
        {
            get;
            set;
        }
        private Func<JObject, Type> _fieldTypeSelector = null;
        public Func<JObject, Type> FieldTypeSelector
        {
            get { return _fieldTypeSelector; }
            set { if (value == null) return; _fieldTypeSelector = value; }
        }

        internal PropertyInfo PI;
        internal object[] PropConverters;
        internal object[] PropConverterParams;

        public ChoJSONRecordFieldConfiguration(string name, string jsonPath = null) : this(name, (ChoJSONRecordFieldAttribute)null)
        {
            JSONPath = jsonPath;
        }

        internal ChoJSONRecordFieldConfiguration(string name, ChoJSONRecordFieldAttribute attr = null, Attribute[] otherAttrs = null) : base(name, attr, otherAttrs)
        {
            IsArray = false;
            FieldName = name;
            if (attr != null)
            {
                JSONPath = attr.JSONPath;
                UseJSONSerialization = attr.UseJSONSerializationInternal;
                FieldName = attr.FieldName.IsNullOrWhiteSpace() ? Name.NTrim() : attr.FieldName.NTrim();
            }
        }

        internal void Validate(ChoJSONRecordConfiguration config)
        {
            try
            {
                if (FieldName.IsNullOrWhiteSpace())
                    FieldName = Name;

                //if (JSONPath.IsNullOrWhiteSpace())
                //    throw new ChoRecordConfigurationException("Missing XPath.");
                if (FillChar != null)
                {
                    if (FillChar.Value == ChoCharEx.NUL)
                        throw new ChoRecordConfigurationException("Invalid '{0}' FillChar specified.".FormatString(FillChar));
                }

                if (Size != null && Size.Value <= 0)
                    throw new ChoRecordConfigurationException("Size must be > 0.");
                if (ErrorMode == null)
                    ErrorMode = config.ErrorMode; // config.ErrorMode;
                if (IgnoreFieldValueMode == null)
                    IgnoreFieldValueMode = config.IgnoreFieldValueMode;
                if (QuoteField == null)
                    QuoteField = config.QuoteAllFields;
            }
            catch (Exception ex)
            {
                throw new ChoRecordConfigurationException("Invalid configuration found at '{0}' field.".FormatString(Name), ex);
            }
        }

        internal bool IgnoreFieldValue(object fieldValue)
        {
            if ((IgnoreFieldValueMode & ChoIgnoreFieldValueMode.Null) == ChoIgnoreFieldValueMode.Null && fieldValue == null)
                return true;
            else if ((IgnoreFieldValueMode & ChoIgnoreFieldValueMode.DBNull) == ChoIgnoreFieldValueMode.DBNull && fieldValue == DBNull.Value)
                return true;
            else if ((IgnoreFieldValueMode & ChoIgnoreFieldValueMode.Empty) == ChoIgnoreFieldValueMode.Empty && fieldValue is string && ((string)fieldValue).IsEmpty())
                return true;
            else if ((IgnoreFieldValueMode & ChoIgnoreFieldValueMode.WhiteSpace) == ChoIgnoreFieldValueMode.WhiteSpace && fieldValue is string && ((string)fieldValue).IsNullOrWhiteSpace())
                return true;

            return false;
        }
    }
}
