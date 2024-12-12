using System.ComponentModel.DataAnnotations;

namespace Aspros.SaaS.System.Domain
{
    public enum UserType
    {
        [Display(Description = "买家")]
        Buyer = 1,
        [Display(Description = "卖家")]
        Seller = 2
    }
}
