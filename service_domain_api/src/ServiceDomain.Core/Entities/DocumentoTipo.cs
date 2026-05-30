namespace ServiceDomain.Core.Entities
{
    public enum DocumentoTipo
    {
        EncomendaCliente = 1,    // Sales Order (Venda)
        EncomendaFornecedor = 2, // Purchase Order (Compra)
        GuiaRemessa = 3,         // Picking confirmation (Saída)
        GuiaRececao = 4          // Goods receipt (Entrada)
    }
}
