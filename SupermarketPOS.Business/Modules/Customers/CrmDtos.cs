using System.Collections.Generic;

namespace SupermarketPOS.Business
{
    public class PipelineStageDto
    {
        public string StageName { get; set; }
        public int Count { get; set; }
        public decimal TotalValue { get; set; }
        public List<PipelineItemDto> Items { get; set; }
    }

    public class PipelineItemDto
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public string CustomerName { get; set; }
        public decimal Amount { get; set; }
        public int Probability { get; set; }
        public string Stage { get; set; }
    }

    public class LeadSummaryDto
    {
        public int TotalLeads { get; set; }
        public int NewLeads { get; set; }
        public int QualifiedLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public decimal ConversionRate { get; set; }
    }
}
