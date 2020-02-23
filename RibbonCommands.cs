﻿
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using WApplication = System.Windows.Forms;
using System.Collections.Specialized;
using System.Threading;
using System.Diagnostics;
using acad = Autodesk.AutoCAD.ApplicationServices.Application;
using Ap = Autodesk.AutoCAD.ApplicationServices;
using Db = Autodesk.AutoCAD.DatabaseServices;
using Ed = Autodesk.AutoCAD.EditorInput;
using Rt = Autodesk.AutoCAD.Runtime;
using Gm = Autodesk.AutoCAD.Geometry;
using Wn = Autodesk.AutoCAD.Windows;
using Hs = Autodesk.AutoCAD.DatabaseServices.HostApplicationServices;
using Us = Autodesk.AutoCAD.DatabaseServices.SymbolUtilityServices;
using Br = Autodesk.AutoCAD.BoundaryRepresentation;
using Pt = Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.PlottingServices;
using System.Collections.Specialized;
using System;
using System.Windows.Forms;

using System.IO.Ports;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using System.Text;
using System.Linq;
using System.Xml.Serialization;

namespace PrintWizard
{
   
    public class TextboxCommandHandler : System.Windows.Input.ICommand
    {
#pragma warning disable 67
        public event EventHandler CanExecuteChanged;
#pragma warning restore 67

        public bool CanExecute(object parameter)
        {
            return true;
        }
        public void Execute(object parameter)
        {
            if (parameter is Autodesk.Windows.RibbonTextBox tb)
            {
                switch (tb.Name)
                {
                    case "tbBlockName":
                        RibbonCommands.blockName = tb.Text;
                        break;
                    case "tbAttrLabel":
                        RibbonCommands.attrLabelName = tb.Text;
                        break;
                    case "tbAttrSheet":
                        RibbonCommands.attrSheetName = tb.Text;
                        break;
                }
            }
        }
    }
    public class ButtonCommandHandler : System.Windows.Input.ICommand
    {
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object param)
        {
            return true;
        }
        public void Execute(object parameter)
        {
            if (parameter is RibbonCommandItem ribbonItem)
            {
                var doc = acad.DocumentManager.MdiActiveDocument;
                //Make sure the command text either ends with ";", or a " "
                string cmdText = ((string)ribbonItem.CommandParameter).Trim();
                if (!cmdText.EndsWith(";"))
                    cmdText += " ";
                doc.SendStringToExecute(cmdText, true, false, true);
            }
        }
    }

    public class ButtonChooseBlockCommandHandler : System.Windows.Input.ICommand
    {
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object param)
        {
            return true;
        }
        public void Execute(object parameter)
        {
            if (parameter is RibbonCommandItem ribbonItem)
            {
                Ap.Document doc = acad.DocumentManager.MdiActiveDocument;
                if (doc == null || doc.IsDisposed)
                    return;
                Ed.Editor ed = doc.Editor;

                using (doc.LockDocument())
                {

                    Ed.PromptEntityOptions peo = new Ed.PromptEntityOptions("\nВыберите экземпляр вхождения блока:");

                    peo.SetRejectMessage("\nВыбранный объект не является вхождением блока.\n");
                    peo.AddAllowedClass(typeof(Db.BlockReference), false);

                    Ed.PromptEntityResult res = ed.GetEntity(peo);

                    if (res.Status != Ed.PromptStatus.OK)
                    {
                        ed.WriteMessage("\nОтмена.\n");
                        return;
                    }

                    Db.ObjectId objId = res.ObjectId;
                    Db.Database db = doc.Database;

                    using (Db.Transaction tr = db.TransactionManager.StartTransaction())
                    {

                        BlockReference br = tr.GetObject(objId, Db.OpenMode.ForRead) as BlockReference;
                        ed.WriteMessage($"\nВыбран блок '{br.Name}'.\n");

                        RibbonCommands.blockName = br.Name;
                        Autodesk.Windows.RibbonControl ribbon = ComponentManager.Ribbon;

                        foreach (var tab in ribbon.Tabs)
                        {
                            if (tab.Title.Equals("Вывод"))
                            {

                                var tb = tab.FindItem("tbBlockName") as RibbonTextBox;
                                if (tb is RibbonTextBox)
                                {
                                    tb.TextValue = RibbonCommands.blockName;
                                }

                                List<string> attrCollection = new List<string>();
                                foreach (ObjectId obj in br.AttributeCollection)
                                {
                                    var attr = tr.GetObject(obj, OpenMode.ForRead) as AttributeReference;
                                    if (attr == null) 
                                            continue;
                                    attrCollection.Add(attr.Tag);
                                }

                                AttributesSelector attrSelector = new AttributesSelector(attrCollection);
                                attrSelector.ShowDialog();

                                RibbonCommands.attrLabelName = AttributesSelector._attrLabel;
                                RibbonCommands.attrSheetName = AttributesSelector._attrSheet;
                                tb = tab.FindItem("tbAttrLabel") as RibbonTextBox;
                                if (tb is RibbonTextBox)
                                    tb.TextValue = RibbonCommands.attrLabelName;
                                tb = tab.FindItem("tbAttrSheet") as RibbonTextBox;
                                if (tb is RibbonTextBox)
                                    tb.TextValue = RibbonCommands.attrSheetName;
                                break;
                            }
                        }
                        PlotWizard.MyBlock_Name = RibbonCommands.blockName;
                        PlotWizard.MyBlockAttr_Label = RibbonCommands.attrLabelName;
                        PlotWizard.MyBLockAttr_Sheet = RibbonCommands.attrSheetName;
                        tr.Commit();
                    }
                }
            }
        }
        private partial class AttributesSelector : Form
        {
            public static string _attrLabel;
            public static string _attrSheet;
            public static List<string> _attrCollection = new List<string>();
            public AttributesSelector(List<string> attrCollection)
            {
                if (attrCollection != null)
                {
                    _attrCollection = attrCollection;
                }
                this.InitializeComponent();
            }

            private System.Windows.Forms.ListBox lbAttributesLabel;
            private System.Windows.Forms.ListBox lbAttributesSheet;
            private System.Windows.Forms.Label labelAttributesLabel;
            private System.Windows.Forms.Label labelAttributesSheet;
            private System.Windows.Forms.Button buttonOk;
            private void InitializeComponent()
            {
                SuspendLayout();

                labelAttributesLabel = new System.Windows.Forms.Label
                {
                    Text = "Атрибут блока -\nчертеж",
                    Location = new System.Drawing.Point(10, 10),
                    Size = new System.Drawing.Size(150, 35)
                };
                
                labelAttributesSheet = new System.Windows.Forms.Label
                {
                    Text = "Атрибут блока -\nлист",
                    Location = new System.Drawing.Point(160, 10),
                    Size = new System.Drawing.Size(150, 35)
                };

                lbAttributesLabel = new System.Windows.Forms.ListBox
                {
                    Location = new System.Drawing.Point(10, 50),
                    Size = new System.Drawing.Size(140, 200),
                };

                lbAttributesSheet = new System.Windows.Forms.ListBox
                {
                    Location = new System.Drawing.Point(160, 50),
                    Size = new System.Drawing.Size(140, 200),
                };
                lbAttributesLabel.Items.Add("Нет");
                lbAttributesSheet.Items.Add("Нет");

                foreach (var _attr in _attrCollection)
                {
                    lbAttributesLabel.Items.Add(_attr);
                    lbAttributesSheet.Items.Add(_attr);
                }

                lbAttributesLabel.SelectedIndex = 0;
                lbAttributesSheet.SelectedIndex = 0;

                buttonOk = new System.Windows.Forms.Button
                {
                    Location = new System.Drawing.Point(225, 280),
                    Size = new System.Drawing.Size(75, 20),
                    Text = "OK"
                };

                lbAttributesLabel.SelectedIndexChanged += new System.EventHandler(lbAttributesLabel_SelectedIndexChanged);
                lbAttributesSheet.SelectedIndexChanged += new System.EventHandler(lbAttributesSheet_SelectedIndexChanged);
                buttonOk.Click += new System.EventHandler(buttonOk_Click);

                AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
                ClientSize = new System.Drawing.Size(310, 320);
                Controls.Add(lbAttributesLabel);
                Controls.Add(lbAttributesSheet);
                Controls.Add(labelAttributesLabel);
                Controls.Add(labelAttributesSheet);
                Controls.Add(buttonOk);

                //System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AttributesSelector));
                Icon = Icon.FromHandle(Properties.Resources.icon_12.GetHicon());
                Text = "Выберите атрибуты блока...";
    
                PerformLayout();    
            }
            private void buttonOk_Click(object sender, EventArgs e)
            {
                if (!String.IsNullOrEmpty(_attrLabel))
                    PlotWizard.MyBlockAttr_Label = _attrLabel;
                if (!String.IsNullOrEmpty(_attrSheet))
                    PlotWizard.MyBLockAttr_Sheet = _attrSheet;
                Close();
            }
            private void lbAttributesLabel_SelectedIndexChanged(object sender, EventArgs e)
            {
                if (!this.lbAttributesLabel.Text.Equals("Нет"))
                {
                    _attrLabel = this.lbAttributesLabel.Text;
                }
                else
                {
                    _attrLabel = " ";
                }
            }
            private void lbAttributesSheet_SelectedIndexChanged(object sender, EventArgs e)
            {
                if (!this.lbAttributesSheet.Text.Equals("Нет"))
                {
                    _attrSheet = this.lbAttributesSheet.Text;
                }
                else
                {
                    _attrSheet = " ";
                }
            }
        }
    }
    public class RibbonCommands : IExtensionApplication
    {
        public static string blockName;
        public static string attrLabelName;
        public static string attrSheetName;
        

        // Функции Initialize() и Terminate() необходимы, чтобы реализовать интерфейс IExtensionApplication
        public void Initialize() { }
        public void Terminate() { }
       
        public void AddMyRibbonPanel()
        {
            RibbonLabel labelBlockName = new RibbonLabel
            {
                Text = "Имя блока для печати  ",
                Height = 22
            };
            RibbonLabel labelAttrLabelName = new RibbonLabel
            {
                Text = "Имя атрибута - чертеж  ",
                Height = 22,
            };
            
            RibbonLabel labelAttrSheetName = new RibbonLabel
            {
                Text = "Имя атрибута - лист  ",
                Height = 22,
            };

            RibbonLabel labelPlotParameters = new RibbonLabel
            {
                Text = "Параметры вывода: ",
                Height = 22,
            };

            Autodesk.Windows.RibbonTextBox tbBlockName = new RibbonTextBox
            {
                Id = "tbBlockName",
                IsEmptyTextValid = false,
                AcceptTextOnLostFocus = true,
                InvokesCommand = true,
                CommandHandler = new TextboxCommandHandler(),
                Width = 100,
                Height = 22,
                Size = RibbonItemSize.Large,
                IsEnabled = false,
                Text = "",
            };

            Autodesk.Windows.RibbonTextBox tbAttrLabel = new RibbonTextBox
            {
                Id = "tbAttrLabel",
                IsEmptyTextValid = false,
                AcceptTextOnLostFocus = true,
                InvokesCommand = true,
                CommandHandler = new TextboxCommandHandler(),
                Width = 100,
                Height = 22,
                Size = RibbonItemSize.Large,
                IsEnabled = false,
                Text = ""
            };

            Autodesk.Windows.RibbonTextBox tbAttrSheet = new RibbonTextBox
            {
                Id = "tbAttrSheet",
                IsEmptyTextValid = false,
                AcceptTextOnLostFocus = true,
                InvokesCommand = true,
                CommandHandler = new TextboxCommandHandler(),
                Width = 100,
                Height = 22,
                Size = RibbonItemSize.Large,
                IsEnabled = false,
                Text = ""
            };

            Autodesk.Windows.RibbonCombo comboPlotterType = new RibbonCombo
            {
                Id = "comboPlotterType",
                Width = 250,
                Height = 22,
                Size = RibbonItemSize.Large,
                Image = Extensions.GetBitmap(Properties.Resources.icon_13),
                LargeImage = Extensions.GetBitmap(Properties.Resources.icon_13)
            };
            foreach (var plotter in Extensions.GetPlotterNameList())
            {
                Autodesk.Windows.RibbonButton btn = new Autodesk.Windows.RibbonButton
                {
                    Text = plotter,
                    ShowText = true
                };
                comboPlotterType.Items.Add(btn);
            }

            Autodesk.Windows.RibbonCombo comboSheetSize = new RibbonCombo
            {
                Id = "comboSheetSize",
                Width = 250,
                Height = 22,
                Size = RibbonItemSize.Large,
                Image = Extensions.GetBitmap(Properties.Resources.icon_14),
                LargeImage = Extensions.GetBitmap(Properties.Resources.icon_14)
            };
            foreach (var sheetSize in Extensions.GetMediaNameList())
            {
                Autodesk.Windows.RibbonButton btn = new Autodesk.Windows.RibbonButton
                {
                    Text = sheetSize.Key.ToString(),
                    ShowText = true
                };
                comboSheetSize.Items.Add(btn);
            }

            Autodesk.Windows.RibbonButton btnChooseBlock = new Autodesk.Windows.RibbonButton
            {
                CommandHandler = new ButtonChooseBlockCommandHandler(),
                Text = "Выбрать\nблок",
                ShowText = true,
                LargeImage = Extensions.GetBitmap(Properties.Resources.icon_12),
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                Width = 65,
                MinWidth = 65
            };

            Autodesk.Windows.RibbonButton btnCreateLayouts = new Autodesk.Windows.RibbonButton
            {
                CommandParameter = "CREATELAYOUTS",
                CommandHandler = new ButtonCommandHandler(),
                Text = "Создать\nлисты",
                ShowText = true,
                LargeImage = Extensions.GetBitmap(Properties.Resources.icon_15),
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                Width = 65,
                MinWidth = 65
            };

            Autodesk.Windows.RibbonButton btnEraseLayouts = new Autodesk.Windows.RibbonButton
            {
                CommandParameter = "ERASEALLLAYOUTS",
                CommandHandler = new ButtonCommandHandler(),
                Text = "Удалить\nлисты",
                ShowText = true,
                LargeImage = Extensions.GetBitmap(Properties.Resources.icon_16),
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                Width = 75,
                MinWidth = 65
            };

            RibbonRowPanel row1 = new RibbonRowPanel();
            row1.Items.Add(labelBlockName);
            row1.Items.Add(new RibbonRowBreak());
            row1.Items.Add(labelAttrLabelName);
            row1.Items.Add(new RibbonRowBreak());
            row1.Items.Add(labelAttrSheetName);
            
            RibbonRowPanel row2 = new RibbonRowPanel();
            row2.Items.Add(tbBlockName);
            row2.Items.Add(new RibbonRowBreak());
            row2.Items.Add(tbAttrLabel);
            row2.Items.Add(new RibbonRowBreak());
            row2.Items.Add(tbAttrSheet);
           
            RibbonRowPanel row3 = new RibbonRowPanel();
            row3.Items.Add(labelPlotParameters);
            row3.Items.Add(new RibbonRowBreak());
            row3.Items.Add(comboPlotterType);
            row3.Items.Add(new RibbonRowBreak());
            row3.Items.Add(comboSheetSize);

            Autodesk.Windows.RibbonPanelSource panelSource = new Autodesk.Windows.RibbonPanelSource()
            {
                Title = "Печать блоков"
            };
            Autodesk.Windows.RibbonPanel plotWizardPanel = new RibbonPanel
            {
                Source = panelSource,
                Id = "plotwizard"
            };
            panelSource.Items.Add(btnChooseBlock);
            panelSource.Items.Add(new RibbonSeparator());
            panelSource.Items.Add(row1);
            panelSource.Items.Add(row2);
            panelSource.Items.Add(new RibbonSeparator());
            panelSource.Items.Add(btnCreateLayouts);
            panelSource.Items.Add(btnEraseLayouts);
            panelSource.Items.Add(new RibbonSeparator());
            panelSource.Items.Add(row3);

            Autodesk.Windows.RibbonControl ribbon = ComponentManager.Ribbon;
            foreach (var tab in ribbon.Tabs)
            {
                if (tab.Title.Equals("Вывод"))
                {
                    tab.Panels.Add(plotWizardPanel);
                    tab.IsActive = true;
                    break;
                }
            }

        }

    }
}
