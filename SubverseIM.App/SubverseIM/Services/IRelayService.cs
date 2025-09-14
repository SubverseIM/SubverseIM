using SIPSorcery.SIP;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IRelayService
    {
        Task<SIPMessageBase> GetNextMessageAsync();
    }
}
