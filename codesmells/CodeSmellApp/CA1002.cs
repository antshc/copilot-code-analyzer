using System;
using System.Collections.Generic;

namespace CodeSmellApp
{
    // This class violates CA1002 by exposing a generic List<T> in its public API.
    public class ViolatesCa1002
    {
        public List<string> Iitems { get; set; }

        public ViolatesCa1002()
        {
            Iitems = new List<string>();
        }

        public void AddItem(string item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            Iitems.Add(item);
        }

        public void RemoveItem(string item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            Iitems.Remove(item);
        }
    }
}
