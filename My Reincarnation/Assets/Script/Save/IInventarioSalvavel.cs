using System.Collections.Generic;

public interface IInventarioSalvavel
{
    List<InventorySaveData> SalvarInventario();
    void CarregarInventario(List<InventorySaveData> itens);
}
