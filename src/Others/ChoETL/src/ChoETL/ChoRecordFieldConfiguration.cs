﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

#if !NETSTANDARD2_0
using System.Windows.Data;
#endif

namespace ChoETL
{
    [DataContract]
    public abstract class ChoRecordFieldConfiguration
    {
        [DataMember]
        public string Name
        {
            get;
            private set;
        }
        [DataMember]
        public ChoErrorMode? ErrorMode
        {
            get;
            set;
        }
        [DataMember]
        public ChoIgnoreFieldValueMode? IgnoreFieldValueMode
        {
            get;
            set;
        }
        [DataMember]
        public string FieldTypeName
        {
            get { return FieldType != null ? FieldType.FullName : null; }
            set { FieldType = value != null ? Type.GetType(value) : null; }
        }
        [DataMember]
        public Type FieldType
        {
            get;
            set;
        }
        [DataMember]
        public bool IsNullable
        {
            get;
            set;
        }
        [DataMember]
        public string FormatText
        {
            get;
            set;
        }

        public ValidationAttribute[] Validators
        {
            get;
            set;
        }
        public Func<object, object> ValueConverter
        {
            get;
            set;
        }
        public Func<dynamic, object> ValueSelector
        {
            get;
            set;
        }
        public Func<string> HeaderSelector
        {
            get;
            set;
        }
        public Func<object, object> CustomSerializer
        {
            get;
            set;
        }
        public Func<object, object> ItemConverter
        {
            get;
            set;
        }

        [IgnoreDataMember]
        public bool IsDefaultValueSpecified
        {
            get;
            internal set;
        }

        private object _defaultValue;
        public object DefaultValue
        {
            get { return _defaultValue; }
            set
            {
                _defaultValue = value;
                IsDefaultValueSpecified = true;
            }
        }

        [IgnoreDataMember]
        public bool IsFallbackValueSpecified
        {
            get;
            internal set;
        }

        private object _fallbackValue;
        public object FallbackValue
        {
            get { return _fallbackValue; }
            set
            {
                _fallbackValue = value;
                IsFallbackValueSpecified = true;
            }
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

        internal readonly List<object> Converters = new List<object>();
        internal PropertyInfo PI;
        internal PropertyDescriptor PD;
        internal object[] PropConverters;
        internal object[] PropConverterParams;

        public ChoRecordFieldConfiguration(string name, ChoRecordFieldAttribute attr = null, Attribute[] otherAttrs = null)
        {
            ChoGuard.ArgumentNotNullOrEmpty(name, "Name");
            Name = name.NTrim();
            if (!ChoETLFrxBootstrap.IsSandboxEnvironment)
                Initialize();

            //FieldType = typeof(string);

            if (attr != null)
            {
                ErrorMode = attr.ErrorModeInternal;
                IgnoreFieldValueMode = attr.IgnoreFieldValueModeInternal;
                FieldType = attr.FieldType;
                IsNullable = attr.IsNullable;
                FormatText = attr.FormatText;
            }
        }

        private void Initialize()
        {
            Name = Name.NTrim().FixName();
        }

#if !NETSTANDARD2_0
        public void AddConverter(IValueConverter converter)
        {
            if (converter == null) return;
            Converters.Add(converter);
        }
#endif
        public void AddConverter(IChoValueConverter converter)
        {
            if (converter == null) return;
            Converters.Add(converter);
        }

        public void AddConverter(TypeConverter converter)
        {
            if (converter == null) return;
            Converters.Add(converter);
        }

        public void RemoveConverter(IChoValueConverter converter)
        {
            if (converter == null) return;
            if (Converters.Contains(converter))
                Converters.Remove(converter);
        }

#if !NETSTANDARD2_0
        public void RemoveConverter(IValueConverter converter)
        {
            if (converter == null) return;
            if (Converters.Contains(converter))
                Converters.Remove(converter);
        }
#endif
        public void RemoveConverter(TypeConverter converter)
        {
            if (converter == null) return;
            if (Converters.Contains(converter))
                Converters.Remove(converter);
        }
    }
}
