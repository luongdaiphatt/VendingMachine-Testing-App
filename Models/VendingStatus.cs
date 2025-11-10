using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VendingMachineTest.Models
{
    public class VendingStatus
    {
        public string MachineId { get; set; }
        public string SlotId { get; set; } 
        public string Status { get; set; } 
        public decimal? Temperature { get; set; }
        public int? Credit { get; set; } 
    }
}
