using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SubverseIM.Serializers
{
    public interface ISerializer<in T>
    {
        void Serialize(T value);
    }
}
