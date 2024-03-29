﻿//
// BIM IFC library: this library works with Autodesk(R) Revit(R) to export IFC files containing model geometry.
// Copyright (C) 2012-2016  Autodesk, Inc.
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

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Export.Utility;
using Revit.IFC.Export.Toolkit;
using Revit.IFC.Export.Exporter.PropertySet;
using Revit.IFC.Common.Utility;

namespace Revit.IFC.Export.Exporter
{
   /// <summary>
   /// Provides methods to export generic MEP family instances.
   /// </summary>
   class GenericMEPExporter
   {
      /// <summary>
      /// Exports a MEP family instance.
      /// </summary>
      /// <param name="exporterIFC">The ExporterIFC object.</param>
      /// <param name="element">The element.</param>
      /// <param name="geometryElement">The geometry element.</param>
      /// <param name="exportType">The export type of the element.
      /// <param name="ifcEnumType">The sub-type of the element.</param></param>
      /// <param name="productWrapper">The ProductWrapper.</param>
      /// <returns>True if an entity was created, false otherwise.</returns>
      public static bool Export(ExporterIFC exporterIFC, Element element, GeometryElement geometryElement,
          IFCExportType exportType, string ifcEnumType, ProductWrapper productWrapper)
      {
         IFCFile file = exporterIFC.GetFile();
         using (IFCTransaction tr = new IFCTransaction(file))
         {
            // CQ_TODO: Clean up this code by at least factoring it out.

            // If we are exporting a duct segment, we may need to split it into parts by level. Create a list of ranges.
            IList<ElementId> levels = new List<ElementId>();
            IList<IFCRange> ranges = new List<IFCRange>();

            // We will not split duct segments if the assemblyId is set, as we would like to keep the original duct segment
            // associated with the assembly, on the level of the assembly.
            if ((exportType == IFCExportType.IfcDuctSegmentType) &&
               (ExporterCacheManager.ExportOptionsCache.WallAndColumnSplitting) &&
               (element.AssemblyInstanceId == ElementId.InvalidElementId))
            {
               LevelUtil.CreateSplitLevelRangesForElement(exporterIFC, exportType, element, out levels,
                                                          out ranges);
            }

            int numPartsToExport = ranges.Count;
            {
               ElementId catId = CategoryUtil.GetSafeCategoryId(element);

               BodyExporterOptions bodyExporterOptions = new BodyExporterOptions(true, ExportOptionsCache.ExportTessellationLevel.ExtraLow);
               if (0 == numPartsToExport)
               {
                  using (PlacementSetter setter = PlacementSetter.Create(exporterIFC, element))
                  {
                     IFCAnyHandle localPlacementToUse = setter.LocalPlacement;
                     BodyData bodyData = null;
                     using (IFCExtrusionCreationData extraParams = new IFCExtrusionCreationData())
                     {
                        extraParams.SetLocalPlacement(localPlacementToUse);
                        IFCAnyHandle productRepresentation =
                            RepresentationUtil.CreateAppropriateProductDefinitionShape(
                                exporterIFC, element, catId, geometryElement, bodyExporterOptions, null, extraParams, out bodyData);
                        if (IFCAnyHandleUtil.IsNullOrHasNoValue(productRepresentation))
                        {
                           extraParams.ClearOpenings();
                           return false;
                        }

                        ExportAsMappedItem(exporterIFC, element, file, exportType, ifcEnumType, extraParams,
                                           setter, localPlacementToUse, productRepresentation,
                                           productWrapper);
                     }
                  }
               }
               else
               {
                  for (int ii = 0; ii < numPartsToExport; ii++)
                  {
                     using (PlacementSetter setter = PlacementSetter.Create(exporterIFC, element, null, null, levels[ii]))
                     {
                        IFCAnyHandle localPlacementToUse = setter.LocalPlacement;

                        using (IFCExtrusionCreationData extraParams = new IFCExtrusionCreationData())
                        {
                           SolidMeshGeometryInfo solidMeshCapsule =
                               GeometryUtil.GetClippedSolidMeshGeometry(geometryElement, ranges[ii]);

                           IList<Solid> solids = solidMeshCapsule.GetSolids();
                           IList<Mesh> polyMeshes = solidMeshCapsule.GetMeshes();

                           IList<GeometryObject> geomObjects =
                               FamilyExporterUtil.RemoveInvisibleSolidsAndMeshes(element.Document,
                               exporterIFC, solids, polyMeshes);

                           if (geomObjects.Count == 0 && (solids.Count > 0 || polyMeshes.Count > 0))
                              return false;

                           bool tryToExportAsExtrusion = (!exporterIFC.ExportAs2x2 ||
                                                          (exportType == IFCExportType.IfcColumnType));

                           if (exportType == IFCExportType.IfcColumnType)
                           {
                              extraParams.PossibleExtrusionAxes = IFCExtrusionAxes.TryZ;
                           }
                           else
                           {
                              extraParams.PossibleExtrusionAxes = IFCExtrusionAxes.TryXYZ;
                           }

                           BodyData bodyData = null;
                           if (geomObjects.Count > 0)
                           {
                              bodyData = BodyExporter.ExportBody(exporterIFC, element, catId,
                                                                 ElementId.InvalidElementId, geomObjects,
                                                                 bodyExporterOptions, extraParams);
                           }
                           else
                           {
                              IList<GeometryObject> exportedGeometries = new List<GeometryObject>();
                              exportedGeometries.Add(geometryElement);
                              bodyData = BodyExporter.ExportBody(exporterIFC, element, catId,
                                                                 ElementId.InvalidElementId,
                                                                 exportedGeometries, bodyExporterOptions,
                                                                 extraParams);
                           }

                           List<IFCAnyHandle> bodyReps = new List<IFCAnyHandle>();
                           bodyReps.Add(bodyData.RepresentationHnd);

                           IFCAnyHandle productRepresentation =
                               IFCInstanceExporter.CreateProductDefinitionShape(exporterIFC.GetFile(), null,
                                                                                null, bodyReps);
                           if (IFCAnyHandleUtil.IsNullOrHasNoValue(productRepresentation))
                           {
                              extraParams.ClearOpenings();
                              return false;
                           }

                           ExportAsMappedItem(exporterIFC, element, file, exportType, ifcEnumType,
                                              extraParams, setter, localPlacementToUse,
                                              productRepresentation, productWrapper);
                        }
                     }
                  }
               }
            }

            tr.Commit();
         }
         return true;
      }

      private static void ExportAsMappedItem(ExporterIFC exporterIFC, Element element, IFCFile file, IFCExportType exportType, string ifcEnumType, IFCExtrusionCreationData extraParams,
          PlacementSetter setter, IFCAnyHandle localPlacementToUse, IFCAnyHandle productRepresentation, ProductWrapper productWrapper)
      {
         IFCAnyHandle ownerHistory = ExporterCacheManager.OwnerHistoryHandle;
         ElementId typeId = element.GetTypeId();
         ElementType type = element.Document.GetElement(typeId) as ElementType;
         IFCAnyHandle styleHandle = null;

         if (type != null)
         {
            FamilyTypeInfo currentTypeInfo = ExporterCacheManager.TypeObjectsCache.Find(typeId, false, exportType);

            bool found = currentTypeInfo.IsValid();
            if (!found)
            {
               string typeGUID = GUIDUtil.CreateGUID(type);
               string typeName = NamingUtil.GetIFCName(type);
               string typeObjectType = NamingUtil.CreateIFCObjectName(exporterIFC, type);
               string applicableOccurance = NamingUtil.GetObjectTypeOverride(type, typeObjectType);
               string typeDescription = NamingUtil.GetDescriptionOverride(type, null);
               string typeElemId = NamingUtil.CreateIFCElementId(type);

               HashSet<IFCAnyHandle> propertySetsOpt = new HashSet<IFCAnyHandle>();
               IList<IFCAnyHandle> repMapListOpt = new List<IFCAnyHandle>();

               styleHandle = FamilyExporterUtil.ExportGenericType(exporterIFC, exportType, ifcEnumType, typeGUID, typeName,
                   typeDescription, applicableOccurance, propertySetsOpt, repMapListOpt, typeElemId, typeName, element, type);
               if (!IFCAnyHandleUtil.IsNullOrHasNoValue(styleHandle))
               {
                  currentTypeInfo.Style = styleHandle;
                  ExporterCacheManager.TypeObjectsCache.Register(typeId, false, exportType, currentTypeInfo);
               }
            }
            else
            {
               styleHandle = currentTypeInfo.Style;
            }
         }

         string instanceGUID = GUIDUtil.CreateGUID(element);
         string instanceName = NamingUtil.GetIFCName(element);
         string objectType = NamingUtil.CreateIFCObjectName(exporterIFC, element);
         string instanceObjectType = NamingUtil.GetObjectTypeOverride(element, objectType);
         string instanceDescription = NamingUtil.GetDescriptionOverride(element, null);
         string instanceElemId = NamingUtil.CreateIFCElementId(element);
         string instanceTag = NamingUtil.GetTagOverride(element, NamingUtil.CreateIFCElementId(element));

         bool roomRelated = !FamilyExporterUtil.IsDistributionFlowElementSubType(exportType);

         ElementId roomId = ElementId.InvalidElementId;
         if (roomRelated)
         {
            roomId = setter.UpdateRoomRelativeCoordinates(element, out localPlacementToUse);
         }

         IFCAnyHandle instanceHandle = null;

         // For MEP objects
         string exportEntityStr = exportType.ToString();
         Common.Enums.IFCEntityType exportEntity;

         if (String.Compare(exportEntityStr.Substring(exportEntityStr.Length - 4), "Type", true) == 0)
            exportEntityStr = exportEntityStr.Substring(0, (exportEntityStr.Length - 4));
         if (Enum.TryParse(exportEntityStr, out exportEntity))
         {
            // For MEP object creation
            instanceHandle = IFCInstanceExporter.CreateGenericIFCEntity(exportEntity, file, instanceGUID, ownerHistory,
               instanceName, instanceDescription, instanceObjectType, localPlacementToUse, productRepresentation, instanceTag);
         }

         if (IFCAnyHandleUtil.IsNullOrHasNoValue(instanceHandle))
            return;

         if (roomId != ElementId.InvalidElementId)
         {
            //exporterIFC.RelateSpatialElement(roomId, instanceHandle);
            ExporterCacheManager.SpaceInfoCache.RelateToSpace(roomId, instanceHandle);
            productWrapper.AddElement(element, instanceHandle, setter, extraParams, false);
         }
         else
         {
            productWrapper.AddElement(element, instanceHandle, setter, extraParams, true);
         }

         OpeningUtil.CreateOpeningsIfNecessary(instanceHandle, element, extraParams, null, exporterIFC, localPlacementToUse, setter, productWrapper);

         if (!IFCAnyHandleUtil.IsNullOrHasNoValue(styleHandle))
            ExporterCacheManager.TypeRelationsCache.Add(styleHandle, instanceHandle);

         PropertyUtil.CreateInternalRevitPropertySets(exporterIFC, element, productWrapper.GetAllObjects());

         ExporterCacheManager.MEPCache.Register(element, instanceHandle);

         // add to system export cache
         // SystemExporter.ExportSystem(exporterIFC, element, instanceHandle);
      }
   }
}
