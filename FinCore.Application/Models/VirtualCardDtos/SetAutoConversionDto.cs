namespace FinCore.Application.Models.VirtualCardDtos
{
    public class SetAutoConversionDto
    {
        /// <summary>Required. true: payments may be funded from any Active account of the owner, with FX conversion.</summary>
        public bool? Enabled { get; set; }
    }
}
