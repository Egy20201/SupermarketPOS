using SupermarketPOS.Core.Entities;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SupermarketPOS.UI.ViewModels
{
    public class DocumentViewModel : INotifyPropertyChanged
    {
        private DocumentStatus _status;
        private string _errorMessage;

        public event PropertyChangedEventHandler PropertyChanged;

        public DocumentStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(CanEdit));
                    OnPropertyChanged(nameof(CanApprove));
                    OnPropertyChanged(nameof(CanPost));
                    OnPropertyChanged(nameof(CanCancel));
                }
            }
        }

        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case DocumentStatus.Draft: return "مسودة";
                    case DocumentStatus.Sent: return "مرسل";
                    case DocumentStatus.Approved: return "معتمد";
                    case DocumentStatus.Confirmed: return "مؤكد";
                    case DocumentStatus.Posted: return "مرحّل";
                    case DocumentStatus.Shipped: return "مشحون";
                    case DocumentStatus.Delivered: return "تم التسليم";
                    case DocumentStatus.Completed: return "مكتمل";
                    case DocumentStatus.Accepted: return "مقبول";
                    case DocumentStatus.Rejected: return "مرفوض";
                    case DocumentStatus.Cancelled: return "ملغي";
                    case DocumentStatus.PartiallyDelivered: return "تسليم جزئي";
                    default: return Status.ToString();
                }
            }
        }

        public string StatusColor
        {
            get
            {
                switch (Status)
                {
                    case DocumentStatus.Draft: return "#F59E0B";
                    case DocumentStatus.Sent: return "#3B82F6";
                    case DocumentStatus.Confirmed: return "#3B82F6";
                    case DocumentStatus.Approved: return "#3B82F6";
                    case DocumentStatus.Posted: return "#10B981";
                    case DocumentStatus.Completed: return "#6B7280";
                    case DocumentStatus.Accepted: return "#10B981";
                    case DocumentStatus.Rejected: return "#EF4444";
                    case DocumentStatus.Cancelled: return "#EF4444";
                    default: return "#6B7280";
                }
            }
        }

        public bool CanEdit => Status == DocumentStatus.Draft;
        public bool CanApprove => Status == DocumentStatus.Draft || Status == DocumentStatus.Sent;
        public bool CanPost => Status == DocumentStatus.Confirmed || Status == DocumentStatus.Approved;
        public bool CanCancel => Status != DocumentStatus.Posted && Status != DocumentStatus.Completed && Status != DocumentStatus.Cancelled;

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public void ClearError() { ErrorMessage = null; }
        public void ShowError(string message) { ErrorMessage = message; }

        public bool ConfirmAction(string action)
        {
            return MessageBox.Show(
                string.Format("هل أنت متأكد من {0}؟", action),
                "تأكيد",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        public bool IsEditing { get; set; }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}