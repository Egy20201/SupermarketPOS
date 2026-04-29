using System.Windows.Controls;

namespace SupermarketPOS.UI.Controls
{
    public class EnhancedDataGrid : DataGrid
    {
        public EnhancedDataGrid()
        {
            AutoGenerateColumns = false;
            CanUserAddRows = false;
            CanUserDeleteRows = false;
            HeadersVisibility = DataGridHeadersVisibility.Column;
            GridLinesVisibility = DataGridGridLinesVisibility.None;
            SelectionMode = DataGridSelectionMode.Single;
        }
    }
}
