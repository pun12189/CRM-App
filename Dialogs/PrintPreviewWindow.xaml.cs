using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Xps.Packaging;

namespace Tijori.Dialogs
{
    /// <summary>
    /// Interaction logic for PrintPreviewWindow.xaml
    /// </summary>
    public partial class PrintPreviewWindow : Window
    {
        private MemoryStream? _memoryStream;
        private Package? _package;
        private XpsDocument? _xpsDocument;

        public PrintPreviewWindow()
        {
            InitializeComponent();
            Closed += PrintPreviewWindow_Closed;
        }

        public void LoadFlowDocument(FlowDocument doc, string title = "Document Preview")
        {
            Title = title;

            // 1. Create in-memory package stream
            _memoryStream = new MemoryStream();
            _package = Package.Open(_memoryStream, FileMode.Create, FileAccess.ReadWrite);

            // 2. Generate unique pack URI
            string packUriString = $"memorystream://preview_{Guid.NewGuid():N}.xps";
            Uri packUri = new Uri(packUriString);
            PackageStore.AddPackage(packUri, _package);

            _xpsDocument = new XpsDocument(_package, CompressionOption.NotCompressed, packUriString);

            // 3. Render FlowDocument onto XPS
            var docPaginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            var writer = XpsDocument.CreateXpsDocumentWriter(_xpsDocument);
            writer.Write(docPaginator);

            // 4. Bind FixedDocument to the UI Viewer
            DocViewer.Document = _xpsDocument.GetFixedDocumentSequence();
        }

        private void PrintPreviewWindow_Closed(object? sender, EventArgs e)
        {
            try
            {
                _xpsDocument?.Close();
                _package?.Close();
                _memoryStream?.Dispose();
            }
            catch { }
        }
    }
}
