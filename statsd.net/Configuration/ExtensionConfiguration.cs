using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace statsd.net.Configuration
{
  public class ExtensionConfiguration
  {
    public class DynamicSource
    {
      public enum RankingOperation
      {
        Top,
        Bottom
      }

      private string _nameRegex;

      public string NameRegex
      {
        get { return _nameRegex; }
        set
        {
          _nameRegex = value;
          PrecompiledRegex = new Regex(_nameRegex, RegexOptions.Compiled);
        }
      }

      public Regex PrecompiledRegex { get; private set; }
      public int Keep { get; set; }
      public RankingOperation Ranking { get; set; }
    }

    private string _nameAndSourceRegex;
    public string NameAndSourceRegex {
      get { return _nameAndSourceRegex; }
      set
      {
        _nameAndSourceRegex = value;
        PrecompiledNameAndSourceRegex = new Regex(_nameAndSourceRegex, RegexOptions.Compiled);
      } }

    public Regex PrecompiledNameAndSourceRegex { get; private set; }

    public IEnumerable<DynamicSource> DynamicSources { get; set; }
  }
}
