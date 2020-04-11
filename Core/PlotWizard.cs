﻿
using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using acad = Autodesk.AutoCAD.ApplicationServices.Application;
using Ap = Autodesk.AutoCAD.ApplicationServices;
using Ed = Autodesk.AutoCAD.EditorInput;
using Rt = Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;

[assembly: Rt.CommandClass(typeof(PlotWizard.Wizard))]

namespace PlotWizard
{ 
    public static class Wizard
    {        
        public static string MyBlockName { get; set; }
        public static Point3d MyFrameMaxPoint { get; set; }
        public static Point3d MyFrameMinPoint { get; set; }
        public static string MyBlockAttrLabel { get; set; }
        public static string MyBLockAttrSheet { get; set; }
        public static double MyViewportScaling { get; set; }
        public static double MyContentScaling { get; set; }
        public static string MyPlotter { get; set; }
        public static string MyPageSize { get; set; }
        private static ObjectIdCollection Layouts { get; set; } 
        private const string MyPageStyle = "acad.ctb";

        [Rt.CommandMethod("PLOTWIZARD", Rt.CommandFlags.Modal)]
        public static void Plotwizard()
        {
            MyPlotter = "DWG To PDF.pc3";
            MyPageSize = "ISO_full_bleed_A4_(210.00_x_297.00_MM)";
            MyContentScaling = 1.003;
            MyViewportScaling = 1;
            Layouts = new ObjectIdCollection(); // stores the newly-created layouts
            AddMyRibbonPanel();
        }

        [Rt.CommandMethod("CREATELAYOUTS", Rt.CommandFlags.Modal)]
        public static void CreateLayouts()
        {
            Ap.Document doc = acad.DocumentManager.MdiActiveDocument;
            if (doc == null || doc.IsDisposed)
                return;
            Ed.Editor ed = doc.Editor;

            using (doc.LockDocument())
            {
                List<PlotObject> plotObjects = GetBlockReferencesBoundaries(MyBlockName, MyFrameMinPoint, MyFrameMaxPoint);
                LayoutCommands lc = new LayoutCommands();
                Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("BACKGROUNDPLOT", 0);

                foreach (var plotObject in plotObjects)
                {
                    ObjectId lay = lc.CreateMyLayout(MyPageSize, MyViewportScaling, MyContentScaling, MyPageStyle, MyPlotter, plotObject);
                    if (!lay.IsNull)
                        Layouts.Add(lay);
                }
                ed.WriteMessage($"Создано {plotObjects.Count.ToString()} листа(-ов).\n");
            }
            ed.Regen();
        }

        [Rt.CommandMethod("MULTIPLOT", Rt.CommandFlags.Modal)]
        public static void MultiPlot()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Вывод в файл",
                Filter = $"{Autodesk.AutoCAD.PlottingServices.PlotConfigManager.CurrentConfig.DeviceName}|*" +
                            $"{Autodesk.AutoCAD.PlottingServices.PlotConfigManager.CurrentConfig.DefaultFileExtension}",
                FileName = GetInitialFilename(Layouts)
            };
            bool? result = saveFileDialog.ShowDialog();

            if (!result.HasValue || !result.Value)
            {
                doc.Editor.WriteMessage("\nОтмена.\n");
                return;
            }

            MultiSheetPlot.MultiSheetPlotter(MyPageSize, MyPlotter, saveFileDialog.FileName, Layouts);
        }

        [Rt.CommandMethod("ERASEALLLAYOUTS", Rt.CommandFlags.Modal)]
        public static void EraseAllLayouts()
        {
            LayoutCommands.EraseAllLayouts();
            Layouts.Clear();
        }
        private static string GetInitialFilename(ObjectIdCollection layouts)
        {
            string filename = "";
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Transaction tr = db.TransactionManager.StartTransaction();

            if (layouts != null && !layouts.IsDisposed)
            {
                Layout layout = tr.GetObject(layouts[0], OpenMode.ForRead) as Layout;
                filename = layout.LayoutName;
            }
            tr.Commit();
            return filename;
        }

        private static void AddMyRibbonPanel()
        {
            RibbonCommands rbCommands = new RibbonCommands();
            rbCommands.AddMyRibbonPanel();
        }

        private static List<PlotObject> GetBlockReferencesBoundaries(String targetBlock, Point3d minPoint, Point3d maxPoint)
        {
            Document doc = acad.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            // create a collection where stored needed block references
            List<PlotObject> plotObjects = new List<PlotObject>();
            // start transaction
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // get table of all drawing's blocks
                var blockTable = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (blockTable == null)
                    return plotObjects;
                // get table of all drawing's blocks references in current modelspace
                var blockTableRecord = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                if (blockTableRecord == null)
                    return plotObjects;
                // iterate through all blocks' references in current modelspace
                foreach (ObjectId blockRecord in blockTableRecord)
                {                    
                    // get single block reference
                    var block = tr.GetObject(blockRecord, OpenMode.ForRead) as BlockReference;
                    if (block == null)
                        continue;
                    if (!block.Name.Equals(targetBlock, StringComparison.CurrentCultureIgnoreCase))
                        continue;
                    
                    Extents3d? bounds = block.Bounds;
                    PlotObject obj = new PlotObject();
                    if (bounds.HasValue)
                    {
                        obj.Extents = bounds.Value; // Extensions.Strip(bounds.Value);
                    }

                    if (obj.Extents.MaxPoint.X <= maxPoint.X &&
                        obj.Extents.MaxPoint.Y <= maxPoint.Y &&
                        obj.Extents.MinPoint.X >= minPoint.X &&
                        obj.Extents.MinPoint.Y >= minPoint.Y)
                    {
                        // find attributes to create the name of file according to it's attribute values
                        foreach (ObjectId attributeId in block.AttributeCollection)
                        {
                            var attribute = tr.GetObject(attributeId, OpenMode.ForRead) as AttributeReference;
                            if (attribute == null)
                                continue;
                            if (attribute.Tag.Contains(MyBlockAttrLabel))
                            {
                                obj.Label = attribute.TextString;
                            }
                            if (attribute.Tag.Equals(MyBLockAttrSheet, StringComparison.CurrentCultureIgnoreCase))
                                obj.Sheet = attribute.TextString;
                        }
                        plotObjects.Add(obj);
                    }
                }
                tr.Commit();
            }
            return plotObjects;
        }
    }
}