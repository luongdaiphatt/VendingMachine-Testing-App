using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VendingMachineTest.Models
{
    public class VendingCommand
    {
        public string Command { get; set; }
        public string SlotId { get; set; }
        public object Data { get; set; }
    }
}
