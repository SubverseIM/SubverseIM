namespace SubverseIM.Serializers
{
    public class NullSerializer<T> : ISerializer<T>
    {
        public void Serialize(T value) { /* IGNORE */ }
    }
}
