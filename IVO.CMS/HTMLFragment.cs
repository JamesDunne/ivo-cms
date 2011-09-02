using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS
{
    public sealed class HTMLFragment
    {
        private string _fragment;

        public HTMLFragment(string fragment)
        {
            _fragment = fragment;
        }

        public static explicit operator string(HTMLFragment fragment) { return fragment._fragment; }
        public static implicit operator HTMLFragment(string fragment) { return new HTMLFragment(fragment); }
    }
}
