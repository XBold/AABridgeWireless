using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AABridgeWireless
{
    public interface IPageCleanup
    {
        Task CleanupAsync();
    }
}
