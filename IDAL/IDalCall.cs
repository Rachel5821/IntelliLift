using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DBEntities.Model;

namespace IDal
{
    public interface IDalCall
    {
        public List<Call> GetAllCalls();
    }
}
