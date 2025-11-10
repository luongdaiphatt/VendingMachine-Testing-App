namespace VendingMachineTest.Base
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class CustomGenerator : Attribute
    {
        public bool IsConstructorCall { get; private set; }
        public object[] Values { get; private set; }
        public CustomGenerator() : this(true) { }
        public CustomGenerator(object value) : this(false, value) { }
        public CustomGenerator(bool isConstructorCall, params object[] values)
        {
            IsConstructorCall = isConstructorCall;
            Values = values ?? new object[0];
        }
    }
}
