﻿//
// BIM IFC library: this library works with Autodesk(R) Revit(R) to export IFC files containing model geometry.
// Copyright (C) 2012  Autodesk, Inc.
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Export.Utility;
using Revit.IFC.Common.Utility;

namespace Revit.IFC.Export.Exporter.PropertySet.Calculators
{
    /// <summary>
    /// A calculation class to calculate gross area.
    /// </summary>
    class LengthCalculator : PropertyCalculator
    {
        /// <summary>
        /// A double variable to keep the calculated value.
        /// </summary>
        private double m_Length = 0;

        /// <summary>
        /// A static instance of this class.
        /// </summary>
        static LengthCalculator s_Instance = new LengthCalculator();

      /// <summary>
      /// The LengthCalculator instance.
      /// </summary>
      public static LengthCalculator Instance
        {
            get { return s_Instance; }
        }

        /// <summary>
        /// Calculates cross area.
        /// </summary>
        /// <param name="exporterIFC">
        /// The ExporterIFC object.
        /// </param>
        /// <param name="extrusionCreationData">
        /// The IFCExtrusionCreationData.
        /// </param>
        /// <param name="element">
        /// The element to calculate the value.
        /// </param>
        /// <param name="elementType">
        /// The element type.
        /// </param>
        /// <returns>
        /// True if the operation succeed, false otherwise.
        /// </returns>
        public override bool Calculate(ExporterIFC exporterIFC, IFCExtrusionCreationData extrusionCreationData, Element element, ElementType elementType)
        {
            double lengthFromParam = 0;
            ParameterUtil.GetDoubleValueFromElementOrSymbol(element, "IfcQtyLength", out lengthFromParam);
            if (extrusionCreationData == null)
            {
               double length = 0;
               ParameterUtil.GetDoubleValueFromElement(element, BuiltInParameter.EXTRUSION_LENGTH, out length);
               if (length > MathUtil.Eps())
               {
                  m_Length = length;
                  return true;
               }
               else if (lengthFromParam < MathUtil.Eps())
                  return false;
               else
               {
                  m_Length = lengthFromParam;
                  return true;
               }
            }
            else
               m_Length = extrusionCreationData.ScaledLength;

         if (m_Length > MathUtil.Eps())
               return true;
            else
            {
               if (lengthFromParam < MathUtil.Eps())
                  return false;
               else
               {
                  m_Length = lengthFromParam;
                  return true;
               }
            }
        }

        /// <summary>
        /// Gets the calculated double value.
        /// </summary>
        /// <returns>
        /// The double value.
        /// </returns>
        public override double GetDoubleValue()
        {
            return m_Length;
        }
    }
}
