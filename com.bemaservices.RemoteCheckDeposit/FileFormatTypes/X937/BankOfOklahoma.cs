using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using BitMiracle.LibTiff.Classic;

using Rock;
using Rock.Model;

using com.bemaservices.RemoteCheckDeposit.Attributes;
using com.bemaservices.RemoteCheckDeposit.Records.X937;

namespace com.bemaservices.RemoteCheckDeposit.FileFormatTypes
{
    /// <summary>
    /// National Bank of Arizona export file format
    /// </summary>
    [Description( "Processes a batch export for Bank Of Oklahoma." )]
    [Export( typeof( FileFormatTypeComponent ) )]
    [ExportMetadata( "ComponentName", "Bank Of Oklahoma" )]
    public class BankOfOklahoma : X937DSTU
    {

        #region Fields

        protected int _cashHeaderId = 1;
        protected int _bundleId = 0;

        #endregion

        #region System Setting Keys

        /// <summary>
        /// The system setting that contains the last file modifier we used.
        /// </summary>
        protected const string SystemSettingLastFileModifier = "BankOfOklahoma.LastFileModifier";

        /// <summary>
        /// The last item sequence number used for items.
        /// </summary>
        protected const string LastItemSequenceNumberKey = "BankOfOklahoma.LastItemSequenceNumber";

        /// <summary>
        /// The last item sequence number used for items.
        /// </summary>
        protected const string LastAddendumSequenceNumber = "BankOfOklahoma.LastAddendumSequenceNumber";

        /// <summary>
        /// The last item sequence number used for items.
        /// </summary>
        protected const string LastLastBundleIdKey = "BankOfOklahoma.LastBundleId";

        #endregion

        /// <summary>
        /// Gets the next item sequence number.
        /// </summary>
        /// <param name="key">The string representing the system setting key</param>
        /// <returns>An integer that identifies the unique item sequence number that can be used.</returns>
        protected int GetNextSequenceNumber(string key)
        {
            int lastSequence = GetSystemSetting( key ).AsIntegerOrNull() ?? 0;
            int nextSequence = lastSequence + 1;

            SetSystemSetting( key, nextSequence.ToString() );

            return nextSequence;
        }

        public override int MaxItemsPerBundle => 299;

        /// <summary>
        /// Gets the file header record (type 01).
        /// </summary>
        /// <param name="options">Export options to be used by the component.</param>
        /// <returns>
        /// A FileHeader record.
        /// </returns>
        /// 
        /// Customized from base: ImmediateOriginRoutingNumber is derived from "Institution Routing Number" 
        /// attribute, rather than "Account Number" attribute
        protected override FileHeader GetFileHeaderRecord( ExportOptions options )
        {
            var header = base.GetFileHeaderRecord( options );
            header.ImmediateOriginRoutingNumber = Rock.Security.Encryption.DecryptString(GetAttributeValue(options.FileFormat, "InstitutionRoutingNumber"));

            //
            // The combination of the following fields must be unique:
            // DestinationRoutingNumber + OriginatingRoutingNumber + CreationDateTime + FileIdModifier
            //
            // If the last file we sent has the same routing numbers and creation date time then
            // increment the file id modifier.
            //
            var fileIdModifier = "A";
            var hashText = header.ImmediateDestinationRoutingNumber + header.ImmediateOriginRoutingNumber + header.FileCreationDateTime.ToString( "yyyyMMddHHmm" );
            var hash = HashString( hashText );

            //
            // find the last modifier, if there was one.
            //
            var lastModifier = GetSystemSetting( SystemSettingLastFileModifier );
            if ( !string.IsNullOrWhiteSpace( lastModifier ) )
            {
                var components = lastModifier.Split( '|' );

                if ( components.Length == 2 )
                {
                    //
                    // If the modifier is for the same file, increment the file modifier.
                    //
                    if ( components[0] == hash )
                    {
                        fileIdModifier = ( (char)( components[1][0] + 1 ) ).ToString();

                        //
                        // If we have done more than 26 files today, assume we are testing and start back at 'A'.
                        //
                        if ( fileIdModifier[0] > 'Z' )
                        {
                            fileIdModifier = "A";
                        }
                    }
                }
            }

            header.FileIdModifier = fileIdModifier;
            SetSystemSetting( SystemSettingLastFileModifier, string.Join( "|", hash, fileIdModifier ) );

            return header;
        }

        /// <summary>
        /// Gets the cash letter header record (type 10).
        /// </summary>
        /// <param name="options">Export options to be used by the component.</param>
        /// <returns>
        /// A CashLetterHeader record.
        /// </returns>
        protected override CashLetterHeader GetCashLetterHeaderRecord( ExportOptions options )
        {
            var header = base.GetCashLetterHeaderRecord( options );

            header.ClientInstitutionRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "InstitutionRoutingNumber" ) );

            return header;
        }

        /// <summary>
        /// Gets all the bundle records in required for the transactions specified.
        /// </summary>
        /// <param name="options">Export options to be used by the component.</param>
        /// <param name="transactions">The transactions to be exported.</param>
        /// <returns>A collection of records that identify all the exported transactions.</returns>
        /// 
        /// Customized from base: calls "GetBundleHeader()" from this class rather than base class (GetBundleHeader() in this class
        /// returns a different object than base, and therefore couldn't simply be overriden)
        protected override List<Record> GetBundleRecords(ExportOptions options, List<FinancialTransaction> transactions)
        {
            var records = new List<Record>();

            for (int bundleIndex = 0; (bundleIndex * MaxItemsPerBundle) < transactions.Count(); bundleIndex++)
            {
                var bundleRecords = new List<Record>();
                var bundleTransactions = transactions.Skip(bundleIndex * MaxItemsPerBundle)
                    .Take(MaxItemsPerBundle)
                    .ToList();

                //
                // Add the bundle header for this set of transactions.
                //
                bundleRecords.Add(this.GetBundleHeader(options, bundleIndex));

                //
                // Allow subclasses to provide credit detail records (type 61) if they want.
                //
                bundleRecords.AddRange(GetCreditDetailRecords(options, bundleIndex, bundleTransactions));

                //
                // Add records for each transaction in the bundle.
                //
                foreach (var transaction in bundleTransactions)
                {
                    try
                    {
                        bundleRecords.AddRange(GetItemRecords(options, transaction));
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(string.Format("Error processing transaction {0}.", transaction.Id), ex);
                    }
                }

                //
                // Add the bundle control record.
                //
                bundleRecords.Add(GetBundleControl(options, bundleRecords));

                records.AddRange(bundleRecords);
            }

            return records;
        }

        /// <summary>
        /// Gets the bundle header record (type 20).
        /// </summary>
        /// <param name="options">Export options to be used by the component.</param>
        /// <param name="bundleIndex">Number of existing bundle records in the cash letter.</param>
        /// <returns>A BundleHeader record.</returns>    
        /// 
        /// Cutomized from base: returns custom object (hence "new" rather than "override"), also
        /// uses "Institution Routing Number" for "ClientInstitutionRoutingNumber" field, 
        /// uses "AccountNumber" attribute value for "ID" field, fills "ReturnLocationRoutingNumber"
        /// field with zeros instead of using the routing number, and uses the ExportDateTime
        /// to fill the UserField
        protected new BankOfOklahomaBundleHeader GetBundleHeader( ExportOptions options, int bundleIndex )
        {
 
            var routingNumber = Rock.Security.Encryption.DecryptString(GetAttributeValue(options.FileFormat, "RoutingNumber"));
            var accountNumber = Rock.Security.Encryption.DecryptString(GetAttributeValue(options.FileFormat, "AccountNumber"));
            var institutionRoutingNumber = Rock.Security.Encryption.DecryptString(GetAttributeValue(options.FileFormat, "InstitutionRoutingNumber"));

            var header = new BankOfOklahomaBundleHeader
            {
                CollectionTypeIndicator = 1,
                DestinationRoutingNumber = routingNumber,
                ClientInstitutionRoutingNumber = institutionRoutingNumber,
                BusinessDate = options.BusinessDateTime,
                CreationDate = options.ExportDateTime,
                ID = accountNumber,
                SequenceNumber = (bundleIndex + 1).ToString(),
                CycleNumber = string.Empty,
                ReturnLocationRoutingNumber = new string('0', 9),
                UserField = options.ExportDateTime.ToString("HHmm")
            };

            return header;
        }


        /// <summary>
        /// Gets the records that identify a single check being deposited.
        /// </summary>
        /// <param name="options">Export options to be used by the component.</param>
        /// <param name="transaction">The transaction to be deposited.</param>
        /// <returns>
        /// A collection of records.
        /// </returns>
        protected override List<Record> GetItemRecords( ExportOptions options, FinancialTransaction transaction )
        {
            var records = base.GetItemRecords( options, transaction );
            var sequenceNumber = GetNextSequenceNumber(LastItemSequenceNumberKey);

            //
            // Modify the Check Detail Record and Check Image Data records to have
            // a unique item sequence number.
            //
            var checkDetail = records.Where( r => r.RecordType == 25 ).Cast<dynamic>().FirstOrDefault();
            checkDetail.ClientInstitutionItemSequenceNumber = sequenceNumber.ToString( "000000000000000" );

            return records;
        }

        /// <summary>
        /// Gets the item detail records (type 25, 26, 28)
        /// </summary>
        /// <param name="options">Export options to be used by the component.</param>
        /// <param name="transaction">The transaction being deposited.</param>
        /// <returns>A collection of records.</returns>
        protected override List<Record> GetItemDetailRecords(ExportOptions options, FinancialTransaction transaction)
        {
            List<Record> records = base.GetItemDetailRecords(options, transaction);
            var detail = records.Where(r => r.RecordType == 25).Cast<dynamic>().FirstOrDefault();
            var detailA = records.Where(r => r.RecordType == 26).Cast<dynamic>().FirstOrDefault();

            string sequenceNumForAddendums = GetNextSequenceNumber(LastAddendumSequenceNumber).ToString("000000000000000");

            if ( detail != null && detailA != null)
            {
                detailA.BankOfFirstDepositRoutingNumber = detailA.BankOfFirstDepositRoutingNumber.PadLeft(9, '0'); // right justifies number and zero-fills up to 9 digits
                detailA.BankOfFirstDepositItemSequenceNumber = sequenceNumForAddendums;
            }

            //
            // Get the Addendum C record (type 28).
            //
            BankOfOklahomaCheckDetailAddendumC detailC = new BankOfOklahomaCheckDetailAddendumC
            {
                RecordNumber = 1,
                EndorsingBankRoutingNumber = Rock.Security.Encryption.DecryptString(GetAttributeValue(options.FileFormat, "RoutingNumber")),
                EndorsingBankEndorsementDate = options.BusinessDateTime,
                EndorsingBankItemSequenceNumber = sequenceNumForAddendums,
                TruncationIndicator = "Y",
                EndorsingBankConversionIndicator = 2,
                EndorsingBankCorrectionIndicator = " "
            };

            return new List<Record> { detail, detailA, detailC };
        }

        /// <summary>
        /// Gets the image record for a specific transaction image (type 50 and 52).
        /// </summary>
        /// <param name="options">Export options to be used by the component.</param>
        /// <param name="transaction">The transaction being deposited.</param>
        /// <param name="image">The check image scanned by the scanning application.</param>
        /// <param name="isFront">if set to <c>true</c> [is front].</param>
        /// <returns>A collection of records.</returns>
        protected override List<Record> GetImageRecords( ExportOptions options, FinancialTransaction transaction, FinancialTransactionImage image, bool isFront )
        {
            var records = base.GetImageRecords( options, transaction, image, isFront );

            var detail = records.Where( r => r.RecordType == 50 ).Cast<dynamic>().FirstOrDefault();
            var data = records.Where( r => r.RecordType == 52 ).Cast<dynamic>().FirstOrDefault();
            if ( detail != null && data != null )
            {
                detail.DataSize = data.ImageData.Length;
            }
            return records;
        }

        /// <summary>
        /// Hashes the string with SHA256.
        /// </summary>
        /// <param name="contents">The contents to be hashed.</param>
        /// <returns>A hex representation of the hash.</returns>
        protected string HashString( string contents )
        {
            byte[] byteContents = Encoding.Unicode.GetBytes( contents );

            var hash = new System.Security.Cryptography.SHA256CryptoServiceProvider().ComputeHash( byteContents );

            return string.Join( "", hash.Select( b => b.ToString( "x2" ) ).ToArray() );
        }

        protected override Stream ConvertImageToTiffG4( Stream imageStream )
        {
            var bitmap = new Bitmap( imageStream );
            bitmap.SetResolution( 200, 200 );

            var bytes = GetTiffImageBytes( bitmap );

            return new MemoryStream( bytes );
        }

        public static byte[] GetTiffImageBytes( Bitmap img )
        {
            try
            {
                // Invert the white/black bytes
                byte[] raster = GetImageRasterBytes( img );
                for( int k = 0; k < raster.Length; k++ )
                {
                    raster[k] = (byte)( ~raster[k] );
                }

                using ( MemoryStream ms = new MemoryStream() )
                {
                    using ( Tiff tif = Tiff.ClientOpen( "InMemory", "w", ms, new TiffStream() ) )
                    {
                        if ( tif == null )
                            return null;

                        tif.SetField( TiffTag.IMAGEWIDTH, img.Width );
                        tif.SetField( TiffTag.IMAGELENGTH, img.Height );
                        tif.SetField( TiffTag.COMPRESSION, Compression.CCITTFAX4 );
                        tif.SetField( TiffTag.PHOTOMETRIC, Photometric.MINISWHITE );
                        tif.SetField( TiffTag.STRIPOFFSETS, 1 );
                        tif.SetField( TiffTag.ROWSPERSTRIP, img.Height );
                        tif.SetField( TiffTag.STRIPBYTECOUNTS, 1 );
                        tif.SetField( TiffTag.XRESOLUTION, img.HorizontalResolution );
                        tif.SetField( TiffTag.YRESOLUTION, img.VerticalResolution );
                        tif.SetField( TiffTag.RESOLUTIONUNIT, ResUnit.INCH );

                        int tiffStride = tif.ScanlineSize();
                        int stride = raster.Length / img.Height;

                        if ( tiffStride < stride )
                        {
                            // raster stride is bigger than TIFF stride
                            // this is due to padding in raster bits
                            // we need to create correct TIFF strip and write it into TIFF

                            byte[] stripBits = new byte[tiffStride * img.Height];
                            for ( int i = 0, rasterPos = 0, stripPos = 0; i < img.Height; i++ )
                            {
                                System.Buffer.BlockCopy( raster, rasterPos, stripBits, stripPos, tiffStride );
                                rasterPos += stride;
                                stripPos += tiffStride;
                            }

                            // Write the information to the file
                            int n = tif.WriteEncodedStrip( 0, stripBits, stripBits.Length );
                            if ( n <= 0 )
                                return null;
                        }
                        else
                        {
                            // Write the information to the file
                            int n = tif.WriteEncodedStrip( 0, raster, raster.Length );
                            if ( n <= 0 )
                                return null;
                        }
                        
                    }

                    return ms.GetBuffer();
                }
            }
            catch ( Exception )
            {
                return null;
            }
        }

        public static byte[] GetImageRasterBytes( Bitmap img )
        {
            // Specify full image
            Rectangle rect = new Rectangle( 0, 0, img.Width, img.Height );

            Bitmap bmp = img;
            byte[] bits = null;

            try
            {
                // Lock the managed memory
                if ( img.PixelFormat != PixelFormat.Format1bppIndexed )
                    bmp = convertToBitonal( img );

                BitmapData bmpdata = bmp.LockBits( rect, ImageLockMode.ReadOnly, PixelFormat.Format1bppIndexed );

                // Declare an array to hold the bytes of the bitmap.
                bits = new byte[bmpdata.Stride * bmpdata.Height];

                // Copy the sample values into the array.
                Marshal.Copy( bmpdata.Scan0, bits, 0, bits.Length );

                // Release managed memory
                bmp.UnlockBits( bmpdata );
            }
            finally
            {
                if ( bmp != img )
                    bmp.Dispose();
            }

            return bits;
        }

        private static Bitmap convertToBitonal( Bitmap original )
        {
            int sourceStride;
            byte[] sourceBuffer = extractBytes( original, out sourceStride );

            // Create destination bitmap
            Bitmap destination = new Bitmap( original.Width, original.Height,
                PixelFormat.Format1bppIndexed );

            destination.SetResolution( original.HorizontalResolution, original.VerticalResolution );

            // Lock destination bitmap in memory
            BitmapData destinationData = destination.LockBits(
                new Rectangle( 0, 0, destination.Width, destination.Height ),
                ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed );

            // Create buffer for destination bitmap bits
            int imageSize = destinationData.Stride * destinationData.Height;
            byte[] destinationBuffer = new byte[imageSize];

            int sourceIndex = 0;
            int destinationIndex = 0;
            int pixelTotal = 0;
            byte destinationValue = 0;
            int pixelValue = 128;
            int height = destination.Height;
            int width = destination.Width;
            int threshold = 500;

            for ( int y = 0; y < height; y++ )
            {
                sourceIndex = y * sourceStride;
                destinationIndex = y * destinationData.Stride;
                destinationValue = 0;
                pixelValue = 128;

                for ( int x = 0; x < width; x++ )
                {
                    // Compute pixel brightness (i.e. total of Red, Green, and Blue values)
                    pixelTotal = sourceBuffer[sourceIndex + 1] + sourceBuffer[sourceIndex + 2] +
                        sourceBuffer[sourceIndex + 3];

                    if ( pixelTotal > threshold )
                        destinationValue += (byte)pixelValue;

                    if ( pixelValue == 1 )
                    {
                        destinationBuffer[destinationIndex] = destinationValue;
                        destinationIndex++;
                        destinationValue = 0;
                        pixelValue = 128;
                    }
                    else
                    {
                        pixelValue >>= 1;
                    }

                    sourceIndex += 4;
                }

                if ( pixelValue != 128 )
                    destinationBuffer[destinationIndex] = destinationValue;
            }

            Marshal.Copy( destinationBuffer, 0, destinationData.Scan0, imageSize );
            destination.UnlockBits( destinationData );
            return destination;
        }

        private static byte[] extractBytes( Bitmap original, out int stride )
        {
            Bitmap source = null;

            try
            {
                // If original bitmap is not already in 32 BPP, ARGB format, then convert
                if ( original.PixelFormat != PixelFormat.Format32bppArgb )
                {
                    source = new Bitmap( original.Width, original.Height, PixelFormat.Format32bppArgb );
                    source.SetResolution( original.HorizontalResolution, original.VerticalResolution );
                    using ( Graphics g = Graphics.FromImage( source ) )
                    {
                        g.DrawImageUnscaled( original, 0, 0 );
                    }
                }
                else
                {
                    source = original;
                }

                // Lock source bitmap in memory
                BitmapData sourceData = source.LockBits(
                    new Rectangle( 0, 0, source.Width, source.Height ),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb );

                // Copy image data to binary array
                int imageSize = sourceData.Stride * sourceData.Height;
                byte[] sourceBuffer = new byte[imageSize];
                Marshal.Copy( sourceData.Scan0, sourceBuffer, 0, imageSize );

                // Unlock source bitmap
                source.UnlockBits( sourceData );

                stride = sourceData.Stride;
                return sourceBuffer;
            }
            finally
            {
                if ( source != original )
                    source.Dispose();
            }

        }
    }

    // Customized from base: ID Field is right justified
    public class BankOfOklahomaBundleHeader : Record
    {
        [IntegerField(2, 2)]
        public int CollectionTypeIndicator { get; set; }

        [TextField(3, 9)]
        public string DestinationRoutingNumber { get; set; }

        [TextField(4, 9)]
        public string ClientInstitutionRoutingNumber { get; set; }

        [DateField(5)]
        public DateTime BusinessDate { get; set; }

        [DateField(6)]
        public DateTime CreationDate { get; set; }

        [TextField(7, 10, FieldJustification.Right)]
        public string ID { get; set; }

        [TextField(8, 4)]
        public string SequenceNumber { get; set; }

        [TextField(9, 2)]
        public string CycleNumber { get; set; }

        [TextField(10, 9)]
        public string ReturnLocationRoutingNumber { get; set; }

        [TextField(11, 5)]
        public string UserField { get; set; }

        [TextField(12, 12)]
        protected string Reserved { get; set; }

        public BankOfOklahomaBundleHeader()
            : base(20)
        {
        }
    }

    // Added from base
    public class BankOfOklahomaCheckDetailAddendumC : Record
    {
        [IntegerField(2, 2)]
        public int RecordNumber { get; set; }

        [TextField(3, 9)]
        public string EndorsingBankRoutingNumber { get; set; }

        [DateField(4)]
        public DateTime EndorsingBankEndorsementDate { get; set; }

        [TextField(5, 15)]
        public string EndorsingBankItemSequenceNumber { get; set; }

        [TextField(6, 1)]
        public string TruncationIndicator { get; set; }

        [IntegerField(7, 1)]
        public int EndorsingBankConversionIndicator { get; set; }

        [TextField(8, 1)]
        public string EndorsingBankCorrectionIndicator { get; set; }

        [TextField(9, 1)]
        public string ReturnReason { get; set; }


        public BankOfOklahomaCheckDetailAddendumC()
            : base(28)
        {
        }
    }
}
