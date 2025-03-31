using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GoogleAdsEngagement.Models
{
    [Table("GoogleAdsEngagements")]
    public class GoogleAdsEngagementModel
    {
        [Key]
        public int Id { get; set; }
        public int ClientId { get; set; }
        public long? CustomerId { get; set; }
        public long? CampaignId { get; set; }
        public string? Name { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? Clicks { get; set; }
        public int? Impressions { get; set; }
        public Double? AverageCpc { get; set; }
        public Double? Ctr { get; set; }
        public DateTime DateOfRun { get; set; } = DateTime.UtcNow;
    }
}

