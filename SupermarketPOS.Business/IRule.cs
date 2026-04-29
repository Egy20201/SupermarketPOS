namespace SupermarketPOS.Business
{
    public interface IRule
    {
        string Validate(SaleRequest request, SaleRuleContext context);
    }
}
