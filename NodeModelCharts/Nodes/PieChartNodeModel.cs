﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Autodesk.DesignScript.Runtime;
using Dynamo.Controls;
using Dynamo.Graph.Nodes;
using Dynamo.UI.Commands;
using Dynamo.Wpf;
using ProtoCore.AST.AssociativeAST;
using NodeModelCharts.Controls;
using Newtonsoft.Json;
using ChartHelpers;

namespace NodeModelCharts.Nodes
{
    [NodeName("Pie")]
    [NodeCategory("NodeModelCharts.Charts")]
    [NodeDescription("Create a new Pie Chart.")]
    [InPortTypes("List<string>", "List<double>", "List<color>")]
    [OutPortTypes("Dictionary<Label, Value>")]
    [IsDesignScriptCompatible]
    public class PieChartNodeModel : NodeModel
    {
        #region Properties
        private Random rnd = new Random();
        /// <summary>
        /// Pie chart labels.
        /// </summary>
        public List<string> Labels { get; set; }

        /// <summary>
        /// Pie chart values.
        /// </summary>
        public List<double> Values { get; set; }

        /// <summary>
        /// Pie chart color values.
        /// </summary>
        public List<SolidColorBrush> Colors { get; set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Instantiate a new NodeModel instance.
        /// </summary>
        public PieChartNodeModel()
        {
            InPorts.Add(new PortModel(PortType.Input, this, new PortData("labels", "pie chart category labels")));
            InPorts.Add(new PortModel(PortType.Input, this, new PortData("values", "pie chart values to be compared")));
            InPorts.Add(new PortModel(PortType.Input, this, new PortData("colors", "pie chart color values")));

            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("labels:values", "Dictionary containing label:value key-pairs")));

            RegisterAllPorts();

            PortDisconnected += PieChartNodeModel_PortDisconnected;

            ArgumentLacing = LacingStrategy.Disabled;
        }

        [JsonConstructor]
        /// <summary>
        /// Instantiate a new NodeModel instance.
        /// </summary>
        public PieChartNodeModel(IEnumerable<PortModel> inPorts, IEnumerable<PortModel> outPorts) : base(inPorts, outPorts)
        {
            PortDisconnected += PieChartNodeModel_PortDisconnected;
        }
        #endregion

        #region Events
        private void PieChartNodeModel_PortDisconnected(PortModel obj)
        {
            // Clear UI when a input port is disconnected
            Labels = new List<string>();
            Values = new List<double>();
            Colors = new List<SolidColorBrush>();

            /*
            // All ports disconnected return template
            if (InPorts[0].IsConnected == false &&
                InPorts[1].IsConnected == false &&
                InPorts[2].IsConnected == false)
            {
                // TODO
            }
            */

            RaisePropertyChanged("DataUpdated");
        }
        #endregion

        #region databridge
        // Use the VMDataBridge to safely retrieve our input values

        /// <summary>
        /// Register the data bridge callback.
        /// </summary>
        protected override void OnBuilt()
        {
            base.OnBuilt();
            VMDataBridge.DataBridge.Instance.RegisterCallback(GUID.ToString(), DataBridgeCallback);
        }

        /// <summary>
        /// Callback method for DataBridge mechanism.
        /// This callback only gets called when 
        ///     - The AST is executed
        ///     - After the BuildOutputAST function is executed 
        ///     - The AST is fully built
        /// </summary>
        /// <param name="data">The data passed through the data bridge.</param>
        private void DataBridgeCallback(object data)
        {
            // Grab input data which always returned as an ArrayList
            var inputs = data as ArrayList;

            // Each of the list inputs are also returned as ArrayLists
            var keys = inputs[0] as ArrayList;
            var values = inputs[1] as ArrayList;
            var colors = inputs[2] as ArrayList;

            // Only continue if key/values match in length
            if(keys.Count != values.Count || keys.Count < 1)
            {
                return; // TODO - throw exception for warning msg?
            }

            // Update chart properties
            Labels = new List<string>();
            Values = new List<double>();
            Colors = new List<SolidColorBrush>();

            if (colors.Count != keys.Count)
            {
                for (var i = 0; i < keys.Count; i++)
                {
                    Labels.Add((string)keys[i]);
                    Values.Add(System.Convert.ToDouble(values[i]));
                    Color randomColor = Color.FromArgb(255, (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256));
                    SolidColorBrush brush = new SolidColorBrush(randomColor);
                    brush.Freeze();
                    Colors.Add(brush);
                }
            }
            else
            {
                for (var i = 0; i < keys.Count; i++)
                {
                    Labels.Add((string)keys[i]);
                    Values.Add(System.Convert.ToDouble(values[i]));
                    var dynColor = (DSCore.Color)colors[i];
                    var convertedColor = Color.FromArgb(dynColor.Alpha, dynColor.Red, dynColor.Green, dynColor.Blue);
                    SolidColorBrush brush = new SolidColorBrush(convertedColor);
                    brush.Freeze();
                    Colors.Add(brush);
                }
            }

            // Notify UI the data has been modified
            RaisePropertyChanged("DataUpdated");
        }
        #endregion

        #region Methods
        /// <summary>
        /// BuildOutputAst is where the outputs of this node are calculated.
        /// This method is used to do the work that a compiler usually does 
        /// by parsing the inputs List inputAstNodes into an abstract syntax tree.
        /// </summary>
        /// <param name="inputAstNodes"></param>
        /// <returns></returns>
        [IsVisibleInDynamoLibrary(false)]
        public override IEnumerable<AssociativeNode> BuildOutputAst(List<AssociativeNode> inputAstNodes)
        {
            // WARNING!!!
            // Do not throw an exception during AST creation.

            // If inputs are not connected return null
            if (InPorts[0].IsConnected == false ||
                InPorts[1].IsConnected == false ||
                InPorts[2].IsConnected == false)
            {
                return new[]
                {
                    AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), AstFactory.BuildNullNode()),
                };
            }

            AssociativeNode inputNode = AstFactory.BuildFunctionCall(
                new Func<List<string>, List<double>, List<DSCore.Color>, Dictionary<string, double>>(PieChartFunctions.GetNodeInput),
                new List<AssociativeNode> { inputAstNodes[0], inputAstNodes[1], inputAstNodes[2] }
            );

            return new[]
            {
                AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), inputNode),
                    AstFactory.BuildAssignment(
                        AstFactory.BuildIdentifier(AstIdentifierBase + "_dummy"),
                        VMDataBridge.DataBridge.GenerateBridgeDataAst(GUID.ToString(), AstFactory.BuildExprList(inputAstNodes)
                    )
                ),
            };
        }
        #endregion
    }

    /// <summary>
    ///     View customizer for CustomNodeModel Node Model.
    /// </summary>
    public class PieChartNodeView : INodeViewCustomization<PieChartNodeModel>
    {
        /// <summary>
        /// At run-time, this method is called during the node 
        /// creation. Add custom UI element to the node view.
        /// </summary>
        /// <param name="model">The NodeModel representing the node's core logic.</param>
        /// <param name="nodeView">The NodeView representing the node in the graph.</param>
        public void CustomizeView(PieChartNodeModel model, NodeView nodeView)
        {
            var pieChartControl = new PieChartControl(model);
            nodeView.inputGrid.Children.Add(pieChartControl);
        }

        /// <summary>
        /// Here you can do any cleanup you require if you've assigned callbacks for particular 
        /// UI events on your node.
        /// </summary>
        public void Dispose() { }
    }

}
