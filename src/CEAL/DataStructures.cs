using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Main {
  public class DataStructures {
    public record DmonItem(string id, string group, int rank, string title, double value, string timestamp, string systemTimestamp);
  }
}
