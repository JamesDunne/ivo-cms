using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS
{
    public sealed class HtmlFragment
    {
        private string _fragment;

        public HtmlFragment(string fragment)
        {
            _fragment = fragment;
        }

        public static explicit operator string(HtmlFragment fragment) { return fragment._fragment; }
        public static implicit operator HtmlFragment(string fragment) { return new HtmlFragment(fragment); }
    }
}
