using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;
using IVO.Definition.Containers;

namespace IVO.CMS.API.Controllers
{
    public sealed class TreeModel
    {
        // TODO: model binding FROM JSON.
        public TreeID root;
        public ImmutableContainer<TreeID, Tree> trees;
    }
}
