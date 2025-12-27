using UnityEngine;

namespace SemanticKeys
{
    /// <summary>
    /// Optional attribute to restrict a SemanticKey field to a specific domain in the Inspector.
    /// Usage: [SemanticKeyFilter("Stats")] public SemanticKey myStat;
    /// </summary>
    public class SemanticKeyFilterAttribute : PropertyAttribute
    {
        public string DomainName { get; private set; }

        public SemanticKeyFilterAttribute(string domainName)
        {
            DomainName = domainName;
        }
    }
}