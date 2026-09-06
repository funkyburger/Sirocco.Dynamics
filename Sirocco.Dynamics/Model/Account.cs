using System;
using System.Collections.Generic;
using System.Text;

namespace Sirocco.Dynamics.Model
{
    internal class Account : BaseEntity
    {
        public string Name { get; set; }
        public IList<Note> Notes { get; set; }
        public Account? Parent { get; set; }
    }
}
