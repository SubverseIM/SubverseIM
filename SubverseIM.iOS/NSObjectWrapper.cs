using Foundation;

namespace SubverseIM.iOS;

public class NSObjectWrapper : NSObject
{
    public object Context { get; }

    private NSObjectWrapper(object obj) : base()
    {
        Context = obj;
    }

    public static NSObjectWrapper Wrap(object obj)
    {
        return new NSObjectWrapper(obj);
    }
}
