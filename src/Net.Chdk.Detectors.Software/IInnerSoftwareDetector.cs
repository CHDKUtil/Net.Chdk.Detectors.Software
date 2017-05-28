using Net.Chdk.Model.Card;
using Net.Chdk.Model.Software;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Net.Chdk.Detectors.Software
{
    public interface IInnerSoftwareDetector
    {
        IEnumerable<SoftwareInfo> GetSoftware(CardInfo cardInfo, IProgress<double> progress, CancellationToken token);
    }
}
