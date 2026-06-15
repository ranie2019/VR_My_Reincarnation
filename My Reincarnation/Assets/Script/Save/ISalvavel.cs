public interface ISalvavel
{
    string ObterId();
    SceneObjectSaveData SalvarEstado();
    void CarregarEstado(SceneObjectSaveData data);
}
