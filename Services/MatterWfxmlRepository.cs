using System;
using System.Collections.Generic;
using System.Linq;
using Slick_Domain.Models;
using Slick_Domain.Entities;
using System.Text;
using NLog;
using MCE = Slick_Domain.Entities.MatterCustomEntities;
using Slick_Domain.Enums;
using Slick_Domain.Common;
using Slick_Domain.XmlProcessing;
using System.IO;
using Slick_Domain.Extensions;


namespace Slick_Domain.Services
{
    public class MatterWfxmlRepository : SlickRepository
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();

        public MatterWfxmlRepository(SlickContext context) : base(context)
        {

        }

        public static void AddMatterWfxmlToMatter(string filePath, int createMatterWfCompId)
        {

        }
    }
}
