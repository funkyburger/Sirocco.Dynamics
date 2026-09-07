using System;
using System.Collections.Generic;
using System.Text;

namespace Sirocco.Dynamics.Model
{
    internal class Contact : BaseEntity
    {
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public IList<Note> Notes { get; set; } = new List<Note>();
    }
}
