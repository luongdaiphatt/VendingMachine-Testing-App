using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VendingMachineTest.Base
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected ViewModelBase()
        {
            InitializeDefaultValues(this);
        }

        public static void InitializeDefaultValues(object obj)
        {
            var props = from prop in obj.GetType().GetProperties()
                        let attrs = prop.GetCustomAttributes(typeof(CustomGenerator), false)
                        where attrs.Any()
                        select new { Property = prop, Attr = ((CustomGenerator)attrs.First()) };
            foreach (var pair in props)
            {
                object value;
                if (pair.Property.PropertyType.Name == "String")
                {
                    if (pair.Attr.Values.Length <= 0)
                    {
                        value = AutoGenString.GenerateNewGuidId();
                    }
                    else
                    {
                        if (pair.Attr.Values[0].ToString() == "AG_Unique_1")
                        {
                            value = AutoGenString.GenUniqueKey();
                        }
                        else if (pair.Attr.Values[0].ToString() == "AG_Unique_2")
                        {
                            value = AutoGenString.GenUniqueKeyOriginal_BIASED();
                        }
                        else
                            value = AutoGenString.GenerateNewGuidId();
                    }
                }
                else
                {
                    value = !pair.Attr.IsConstructorCall && pair.Attr.Values.Length > 0
                                    ? pair.Attr.Values[0]
                                    : Activator.CreateInstance(pair.Property.PropertyType, pair.Attr.Values);
                }
                pair.Property.SetValue(obj, value, null);
            }
        }
    }
}
