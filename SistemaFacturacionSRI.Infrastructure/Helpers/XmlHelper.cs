using System.Xml.Serialization;

namespace SistemaFacturacionSRI.Infrastructure.Helpers
{
    public static class XmlHelper
    {
        public static T Deserialize<T>(string xmlText)
        {
            if (string.IsNullOrWhiteSpace(xmlText)) return default;

            try
            {
                var serializer = new XmlSerializer(typeof(T));
                using (var reader = new StringReader(xmlText))
                {
                    return (T)serializer.Deserialize(reader);
                }
            }
            catch (Exception)
            {
                // Si falla la deserialización, devolvemos null o manejamos el error
                return default;
            }
        }
    }
}