using System.ComponentModel;
using System.ComponentModel.Composition;

using com.bemaservices.RemoteCheckDeposit.Records.X937;

namespace com.bemaservices.RemoteCheckDeposit.FileFormatTypes
{
    /// <summary>
    /// Exports an X9.37 file conforming to First Citizens Bank's "Commercial
    /// Image Cash Letter" specification (DSTU X9.37-2003 with selected
    /// ANS X9.100-180-2006 artifacts).
    ///
    /// Inherits the generic X937V2DSTU component verbatim except for one
    /// override: the Type 10 Cash Letter Header writes the ECE Institution
    /// Routing Number (Field 4) as a 9-character string so leading zeros
    /// survive. The base class runs the value through int.Parse +
    /// ToStringSafe(), which strips leading zeros and then pads right with
    /// a space, producing "53100300 " from a configured "053100300". The
    /// equivalent code path for Type 20 already treats the value as a string,
    /// so Type 20 is correct out of the box.
    /// </summary>
    [Description( "Processes a batch export for First Citizens Bank." )]
    [Export( typeof( FileFormatTypeComponent ) )]
    [ExportMetadata( "ComponentName", "First Citizens Bank" )]
    public class FirstCitizensBank : X937V2DSTU
    {
        /// <summary>
        /// Overrides the Type 10 Cash Letter Header so the ECE Institution
        /// Routing Number (Field 4) preserves leading zeros. See class summary
        /// for the underlying base-class behavior being worked around.
        /// </summary>
        protected override CashLetterHeader GetCashLetterHeaderRecord( ExportOptions options )
        {
            var header = base.GetCashLetterHeaderRecord( options );

            string institutionRoutingNumber = GetValueWithFallback( options, "InstitutionRoutingNumber", "RoutingNumber" );
            if ( !string.IsNullOrWhiteSpace( institutionRoutingNumber ) )
            {
                header.ClientInstitutionRoutingNumber = institutionRoutingNumber;
            }

            return header;
        }
    }
}
