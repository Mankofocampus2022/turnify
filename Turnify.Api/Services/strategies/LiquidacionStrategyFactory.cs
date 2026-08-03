namespace Turnify.Api.Services.Strategies
{
    public class LiquidacionStrategyFactory
    {
        public static ILiquidacionStrategy ObtenerEstrategia(bool esIndependiente)
        {
            return esIndependiente 
                ? new LiquidacionIndependienteStrategy() 
                : new LiquidacionDependienteStrategy();
        }
    }
}