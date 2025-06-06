﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.UI.WebControls;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.UI;

using com.bemaservices.RemoteCheckDeposit;
using com.bemaservices.RemoteCheckDeposit.Model;
using System.ComponentModel;
using Rock.Web.UI.Controls;
using Rock.Web.Cache;
using System.Text;
using System.IO;
using Rock.Attribute;
using System.Linq.Dynamic;

namespace RockWeb.Plugins.com_bemaservices.RemoteCheckDeposit
{
    [DisplayName( "Remote Deposit Export" )]
    [Category( "BEMA Services > Remote Check Deposit" )]
    [Description( "Exports batch data for use remote deposit with a bank." )]
    [BooleanField( "Show Transaction Details On Grid", defaultValue: false, Order = 1 )]
    public partial class RemoteDepositExport : RockBlock
    {
        enum IsDeposited { Yes, No }

        #region Base Method Overrides

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            gBatches.Actions.ShowMergeTemplate = false;
            gBatches.ShowHeaderWhenEmpty = true;
            gBatches.ShowActionsInHeader = true;

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );

            gBatches.DataKeyNames = new string[] { "Id" };
        }

        private void Block_BlockUpdated( object sender, EventArgs e )
        {
            BindBatchesFilter();
            BindBatchesGrid();
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !IsPostBack )
            {
                var fileFormatService = new ImageCashLetterFileFormatService( new RockContext() );
                var fileFormats = fileFormatService.Queryable().Where( f => f.IsActive == true );

                ddlFileFormat.Items.Clear();
                //ddlFileFormat.Items.Add( new ListItem() );
                foreach ( var fileFormat in fileFormats )
                {
                    ddlFileFormat.Items.Add( new ListItem( fileFormat.Name, fileFormat.Id.ToString() ) );
                }

                BindBatchesFilter();
                BindBatchesGrid();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Binds the batches grid.
        /// </summary>
        private void BindBatchesGrid()
        {
            try
            {
                var rockContext = new RockContext();
                var attributeValueService = new AttributeValueService( rockContext );
                var binaryFileService = new BinaryFileService( rockContext );
                var fileTypeGuid = Rock.SystemGuid.BinaryFiletype.DEFAULT.AsGuid();


                var financialBatchQry = GetQuery( rockContext )
                    .AsNoTracking()
                    .Include( b => b.Campus )
                    .Include( b => b.Transactions.Select( t => t.TransactionDetails ) );

                //var batchIds = financialBatchQry.Select( b => b.Id ).ToList();
                //var depositedQry = attributeValueService.Queryable().AsNoTracking().Where( av => av.Attribute.Key == "com.bemaservices.Deposited" && av.EntityId.HasValue && batchIds.Contains( av.EntityId.Value ) );
                //var exportDateQry = attributeValueService.Queryable().AsNoTracking().Where( av => av.Attribute.Key == "com.bemaservices.ExportDate" && av.EntityId.HasValue && batchIds.Contains( av.EntityId.Value ) );
                //var exportFileQry = attributeValueService.Queryable().AsNoTracking().Where( av => av.Attribute.Key == "com.bemaservices.ExportFile" && av.EntityId.HasValue && batchIds.Contains( av.EntityId.Value ) );
                //var binaryFileQry = binaryFileService.Queryable().AsNoTracking().Where( bf => bf.BinaryFileType.Guid == fileTypeGuid )
                //    .Join( exportFileQry, bf => bf.Guid.ToString(), av => av.Value, ( bf, av ) => new
                //    {
                //        BatchId = av.EntityId,
                //        FileName = bf.FileName,
                //        FileGuid = bf.Guid
                //    } );

                //if ( GetAttributeValue( "ShowTransactionDetailsOnGrid" ).AsBoolean() )
                //{
                //    financialBatchQry = financialBatchQry
                //    .Include( b => b.Transactions.Select( t => t.TransactionDetails ) ) // Load Transactions and TransactionDetails

                //    gBatches.Columns[4].Visible = true;
                //    gBatches.Columns[6].Visible = true;

                //    var batchRowQry = financialBatchQry.Select( b => new BatchRow
                //    {
                //        Id = b.Id,
                //        BatchStartDateTime = b.BatchStartDateTime.Value,
                //        Name = b.Name,
                //        AccountingSystemCode = b.AccountingSystemCode,
                //        TransactionCount = b.Transactions.Count(),
                //        TransactionAmount = b.Transactions.SelectMany( t => t.TransactionDetails ).Sum( d => ( decimal? ) d.Amount ) ?? 0.0M,
                //        ControlAmount = b.ControlAmount,
                //        CampusName = b.Campus != null ? b.Campus.Name : "",
                //        Status = b.Status,
                //        UnMatchedTxns = b.Transactions.Any( t => !t.AuthorizedPersonAliasId.HasValue ),
                //        BatchNote = b.Note,
                //        Deposited = depositedQry.Any( av => av.EntityId == b.Id && av.ValueAsBoolean.HasValue && av.ValueAsBoolean.Value ),
                //        ExportDate = exportDateQry.Any( av => av.EntityId == b.Id ) ? exportDateQry.Where( av => av.EntityId == b.Id ).FirstOrDefault().ValueAsDateTime : null,
                //        ExportFileName = binaryFileQry.Any( av => av.BatchId == b.Id ) ? binaryFileQry.Where( av => av.BatchId == b.Id ).FirstOrDefault().FileName : null,
                //        ExportFileUrl = binaryFileQry.Any( av => av.BatchId == b.Id ) ? "/GetFile.ashx?guid="+ binaryFileQry.Where( av => av.BatchId == b.Id ).FirstOrDefault().FileGuid : null
                //    } );

                //    gBatches.SetLinqDataSource( batchRowQry );
                //}
                //else
                //{                  

                //    gBatches.Columns[4].Visible = false;
                //    gBatches.Columns[6].Visible = false;

                //    var batchRowQry = financialBatchQry.Select( b => new BatchRow
                //    {
                //        Id = b.Id,
                //        BatchStartDateTime = b.BatchStartDateTime.Value,
                //        Name = b.Name,
                //        AccountingSystemCode = b.AccountingSystemCode,
                //        ControlAmount = b.ControlAmount,
                //        TransactionAmount = b.ControlAmount,  // just assume transactions are balanced on batch for now
                //        CampusName = b.Campus != null ? b.Campus.Name : "",
                //        Status = b.Status,
                //        BatchNote = b.Note,
                //        Deposited = depositedQry.Any( av => av.EntityId == b.Id && av.ValueAsBoolean.HasValue && av.ValueAsBoolean.Value ),
                //        ExportDate = exportDateQry.Any( av => av.EntityId == b.Id ) ? exportDateQry.Where( av => av.EntityId == b.Id ).FirstOrDefault().ValueAsDateTime : null,
                //        ExportFileName = binaryFileQry.Any( av => av.BatchId == b.Id ) ? binaryFileQry.Where( av => av.BatchId == b.Id ).FirstOrDefault().FileName : null,
                //        ExportFileUrl = binaryFileQry.Any( av => av.BatchId == b.Id ) ? "/GetFile.ashx?guid=" + binaryFileQry.Where( av => av.BatchId == b.Id ).FirstOrDefault().FileGuid : null
                //    } );

                //    gBatches.SetLinqDataSource( batchRowQry );
                //}

                gBatches.EntityTypeId = EntityTypeCache.Get( Rock.SystemGuid.EntityType.FINANCIAL_BATCH.AsGuid() ).Id;
                gBatches.SetLinqDataSource( financialBatchQry );
                gBatches.DataBind();
            }
            catch ( Exception ex )
            {
                nbWarningMessage.Text = ex.Message;
            }
        }

        /// <summary>
        /// Binds the batches filter.
        /// </summary>
        private void BindBatchesFilter()
        {
            string titleFilter = gfBatches.GetFilterPreference( "Title" );
            tbTitle.Text = !string.IsNullOrWhiteSpace( titleFilter ) ? titleFilter : string.Empty;

            ddlStatus.BindToEnum<BatchStatus>();
            ddlStatus.Items.Insert( 0, Rock.Constants.All.ListItem );
            string statusFilter = gfBatches.GetFilterPreference( "Status" );
            if ( string.IsNullOrWhiteSpace( statusFilter ) )
            {
                statusFilter = BatchStatus.Closed.ConvertToInt().ToString();
            }
            ddlStatus.SetValue( statusFilter );

            var campuses = CampusCache.All();
            campCampus.Campuses = campuses;
            campCampus.Visible = campuses.Any();
            campCampus.SetValue( gfBatches.GetFilterPreference( "Campus" ) );

            drpBatchDate.DelimitedValues = gfBatches.GetFilterPreference( "Date Range" );
            drpExportDate.DelimitedValues = gfBatches.GetFilterPreference( "Export Date Range" );

            ddlDeposited.BindToEnum<IsDeposited>();
            ddlDeposited.Items.Insert( 0, Rock.Constants.All.ListItem );
            string depositedFilter = gfBatches.GetFilterPreference( "Deposited" );
            if ( string.IsNullOrWhiteSpace( depositedFilter ) )
            {
                depositedFilter = IsDeposited.No.ConvertToInt().ToString();
            }
            ddlDeposited.SetValue( depositedFilter );
        }

        /// <summary>
        /// Gets the query.  Set the timeout to 90 seconds in case the user
        /// has not set any filters and they've imported N years worth of
        /// batch data into Rock.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <returns></returns>
        private IOrderedQueryable<FinancialBatch> GetQuery( RockContext rockContext )
        {
            var batchService = new FinancialBatchService( rockContext );
            rockContext.Database.CommandTimeout = 90;
            var qry = batchService.Queryable().AsNoTracking()
                .Where( b => b.BatchStartDateTime.HasValue );

            // filter by date
            string dateRangeValue = gfBatches.GetFilterPreference( "Date Range" );
            if ( !string.IsNullOrWhiteSpace( dateRangeValue ) )
            {
                var drp = new DateRangePicker();
                drp.DelimitedValues = dateRangeValue;
                if ( drp.LowerValue.HasValue )
                {
                    qry = qry.Where( b => b.BatchStartDateTime >= drp.LowerValue.Value );
                }

                if ( drp.UpperValue.HasValue )
                {
                    var endOfDay = drp.UpperValue.Value.AddDays( 1 );
                    qry = qry.Where( b => b.BatchStartDateTime < endOfDay );
                }
            }

            // filter by export date
            string exportDateRangeValue = gfBatches.GetFilterPreference( "Export Date Range" );
            if ( !string.IsNullOrWhiteSpace( exportDateRangeValue ) )
            {
                var drp = new DateRangePicker();
                drp.DelimitedValues = exportDateRangeValue;
                if ( drp.LowerValue.HasValue )
                {
                    qry = qry.WhereAttributeValue( rockContext, av => av.Attribute.Key == "com.bemaservices.ExportDate" && av.ValueAsDateTime.HasValue && av.ValueAsDateTime >= drp.LowerValue.Value );
                }

                if ( drp.UpperValue.HasValue )
                {
                    var endOfDay = drp.UpperValue.Value.AddDays( 1 );
                    qry = qry.WhereAttributeValue( rockContext, av => av.Attribute.Key == "com.bemaservices.ExportDate" && av.ValueAsDateTime.HasValue && av.ValueAsDateTime < endOfDay );
                }
            }

            // filter by status
            var status = gfBatches.GetFilterPreference( "Status" ).ConvertToEnumOrNull<BatchStatus>();
            if ( status.HasValue )
            {
                qry = qry.Where( b => b.Status == status );
            }

            // filter by title
            string title = gfBatches.GetFilterPreference( "Title" );
            if ( !string.IsNullOrEmpty( title ) )
            {
                qry = qry.Where( batch => batch.Name.Contains( title ) );
            }

            // filter by campus
            var campus = CampusCache.Get( gfBatches.GetFilterPreference( "Campus" ).AsInteger() );
            if ( campus != null )
            {
                qry = qry.Where( b => b.CampusId == campus.Id );
            }

            var deposited = gfBatches.GetFilterPreference( "Deposited" ).ConvertToEnumOrNull<IsDeposited>();

            if ( deposited.HasValue )
            {
                var attributeQry = new AttributeValueService( rockContext ).Queryable().AsNoTracking().Where( av => av.Attribute.Key == "com.bemaservices.Deposited" );

                if ( deposited == IsDeposited.Yes )
                {
                    qry = qry.Where( b => attributeQry.Any( av => av.EntityId == b.Id && av.ValueAsBoolean == true ) );
                }
                else if ( deposited == IsDeposited.No )
                {
                    qry = qry.Where( b => !attributeQry.Any( av => av.EntityId == b.Id && av.ValueAsBoolean == true ) );
                }
            }

            IOrderedQueryable<FinancialBatch> sortedQry = null;

            SortProperty sortProperty = gBatches.SortProperty;
            if ( sortProperty != null )
            {
                switch ( sortProperty.Property )
                {
                    case "TransactionCount":
                        {
                            if ( sortProperty.Direction == SortDirection.Ascending )
                            {
                                sortedQry = qry.OrderBy( b => b.Transactions.Count() );
                            }
                            else
                            {
                                sortedQry = qry.OrderByDescending( b => b.Transactions.Count() );
                            }

                            break;
                        }

                    case "TransactionAmount":
                        {
                            if ( sortProperty.Direction == SortDirection.Ascending )
                            {
                                sortedQry = qry.OrderBy( b => b.Transactions.Sum( t => ( decimal? ) ( t.TransactionDetails.Sum( d => ( decimal? ) d.Amount ) ?? 0.0M ) ) ?? 0.0M );
                            }
                            else
                            {
                                sortedQry = qry.OrderByDescending( b => b.Transactions.Sum( t => ( decimal? ) ( t.TransactionDetails.Sum( d => ( decimal? ) d.Amount ) ?? 0.0M ) ) ?? 0.0M );
                            }

                            break;
                        }

                    default:
                        {
                            sortedQry = qry.Sort( sortProperty );
                            break;
                        }
                }
            }
            else
            {
                sortedQry = qry
                    .OrderByDescending( b => b.BatchStartDateTime )
                    .ThenBy( b => b.Name );
            }

            return sortedQry;
        }

        /// <summary>
        /// Formats the value as currency.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        protected string FormatValueAsCurrency( decimal value )
        {
            return value.FormatAsCurrency();
        }

        /// <summary>
        /// Validates the selection to ensure that all MICR numbers are valid.
        /// </summary>
        protected bool ValidateSelection( bool showAll = false )
        {
            var rockContext = new RockContext();
            var batchIds = hfBatchIds.Value.SplitDelimitedValues().AsIntegerList();

            //
            // Check for a bad MICR scan.
            //
            var transactions = new FinancialBatchService( rockContext ).Queryable()
                .Where( b => batchIds.Contains( b.Id ) )
                .SelectMany( b => b.Transactions )
                .ToList()
                .Where( t => string.IsNullOrEmpty( t.CheckMicrHash ) || !Micr.IsValid( Rock.Security.Encryption.DecryptString( t.CheckMicrEncrypted ) ) ||
                        t.Images.Count < 2 )
                .OrderBy( t => t.Id )
                .ToList();

            if ( transactions.Count > 0 || showAll )
            {
                string routingNumber;
                string accountNumber;
                string checkNumber;
                bool isBankCheck;
                List<MicrRow> micrRowList = new List<MicrRow>();

                foreach ( var transaction in new FinancialBatchService( rockContext ).Queryable()
                    .Where( b => batchIds.Contains( b.Id ) )
                    .SelectMany( b => b.Transactions )
                    .ToList() )
                {
                    routingNumber = "";
                    accountNumber = "";
                    checkNumber = "";
                    isBankCheck = false;
                    try
                    {
                        var micr = new Micr( Rock.Security.Encryption.DecryptString( transaction.CheckMicrEncrypted ) );
                        routingNumber = micr.GetRoutingNumber();
                        accountNumber = micr.GetAccountNumber();
                        checkNumber = micr.GetCheckNumber();
                        string micrString = Rock.Security.Encryption.DecryptString( transaction.CheckMicrEncrypted ).Trim();
                        int startC = micrString.IndexOf( 'c' );
                        if ( startC == 0 )
                        {
                            int endC = micrString.Substring( startC + 1 ).IndexOf( 'c' );
                            int length = endC - startC;
                            string possibleBankCheckAux = micrString.Substring( startC + 1, length );
                            if ( !possibleBankCheckAux.ToCharArray().Any( c => c == 'c' || c == 'd' ) ) // check for any breaks or interruptions
                            {
                                isBankCheck = true;
                            }
                        }
                    }
                    catch { /* Intentionally left blank */ }

                    List<string> errors = new List<string>();
                    bool isValid = Micr.IsValid( Rock.Security.Encryption.DecryptString( transaction.CheckMicrEncrypted ), out errors );
                    bool hasImages = transaction.Images.Count >= 2;

                    if ( !hasImages )
                    {
                        errors.Add( "Transaction does not contain two images (front and back of check) " );
                    }

                    MicrRow micrRow = new MicrRow()
                    {
                        TransactionId = transaction.Id,
                        RoutingNumber = routingNumber,
                        AccountNumber = accountNumber,
                        CheckNumber = checkNumber,
                        IsBankCheck = isBankCheck,
                        Amount = transaction.TotalAmount,
                        ImageUrl = transaction.Images.FirstOrDefault() != null ? transaction.Images.First().BinaryFile.Url : "",
                        IsValid = ( isValid && hasImages ),
                        IsValidMessage = errors.AsDelimited( "<br/>" )
                    };

                    micrRowList.Add( micrRow );
                }

                rptMicrDetail.DataSource = micrRowList;
                rptMicrDetail.DataBind();

                pnlBatches.Visible = false;
                pnlFixMicr.Visible = true;

                return false;
            }
            else
            {
                return true;
            }
        }

        protected void ShowOptions()
        {
            var rockContext = new RockContext();
            var batchIds = hfBatchIds.Value.SplitDelimitedValues().AsIntegerList();

            decimal total = new FinancialBatchService( rockContext ).Queryable()
                .Where( b => batchIds.Contains( b.Id ) )
                .ToList()
                .Sum( b => b.Transactions.Sum( t => t.TotalAmount ) );

            lTotalDeposit.Text = total.FormatAsCurrency();

            dpBusinessDate.SelectedDateTime = RockDateTime.Now.SundayDate().AddDays( -7 );

            pnlBatches.Visible = false;
            pnlFixMicr.Visible = false;
            pnlOptions.Visible = true;
        }

        protected void ExportBatches()
        {
            using ( var rockContext = new RockContext() )
            {
                var batchIds = hfBatchIds.Value.SplitDelimitedValues().AsIntegerList();
                var batches = new FinancialBatchService( rockContext ).Queryable()
                    .AsNoTracking()
                    .Where( b => batchIds.Contains( b.Id ) )
                    .ToList();
                var fileFormat = new ImageCashLetterFileFormatService( rockContext ).Get( ddlFileFormat.SelectedValue.AsInteger() );
                var component = FileFormatTypeContainer.GetComponent( fileFormat.EntityType.Name );
                List<string> errorMessages;

                fileFormat.LoadAttributes( rockContext );

                // Create a counter for each day's exports
                string counterKey = "com.bemaservices.RemoteCheckDeposit.CounterKey";
                string valueFromKey = Rock.Web.SystemSettings.GetValue( counterKey );
                int counter = 1;
                if ( valueFromKey.IsNotNullOrWhiteSpace() && valueFromKey.Split( '|' ).Count() == 2 )
                {
                    var keyArray = valueFromKey.Split( '|' );
                    if ( keyArray[0].AsDateTime() >= RockDateTime.Today )
                    {
                        counter = keyArray[1].AsInteger() + 1;
                    }
                }
                //Set key to DateTime of Today with a counter of 1
                string keyString = RockDateTime.Today.ToString() + "|" + counter.ToString();
                Rock.Web.SystemSettings.SetValue( counterKey, keyString );

                //
                // Get the final filename for the export.
                //
                var mergeFields = new Dictionary<string, object>
                {
                    {  "FileFormat", fileFormat },
                    { "Counter", counter }
                };
                var filename = fileFormat.FileNameTemplate.ResolveMergeFields( mergeFields );

                //
                // Construct the export options.
                //
                var options = new ExportOptions( fileFormat, batches );
                options.BusinessDateTime = dpBusinessDate.SelectedDateTime.Value;
                options.ExportDateTime = RockDateTime.Now;

                //
                // Perform the export.
                //
                Stream stream = null;
                try
                {
                    stream = component.ExportBatches( options, out errorMessages );
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                    nbWarningMessage.Text = ex.Message;
                    return;
                }

                //
                // Save the data to the database.
                //
                var binaryFileService = new BinaryFileService( rockContext );
                var binaryFileTypeService = new BinaryFileTypeService( rockContext );
                var binaryFile = new BinaryFile
                {
                    BinaryFileTypeId = binaryFileTypeService.Get( com.bemaservices.RemoteCheckDeposit.SystemGuid.BinaryFileType.REMOTE_CHECK_DEPOSIT.AsGuid() ).Id,
                    IsTemporary = false,
                    FileName = filename,
                    MimeType = "application/octet-stream", //"octet/stream",
                    ContentStream = stream
                };

                binaryFileService.Add( binaryFile );
                rockContext.SaveChanges();

                foreach ( var batch in batches )
                {
                    batch.LoadAttributes();
                    batch.SetAttributeValue( "com.bemaservices.Deposited", "True" );
                    batch.SetAttributeValue( "com.bemaservices.ExportDate", RockDateTime.Now );
                    batch.SetAttributeValue( "com.bemaservices.ExportFile", binaryFile.Guid );
                    batch.SaveAttributeValues();
                }

                //
                // Present download link.
                //
                pnlOptions.Visible = false;
                pnlSuccess.Visible = true;
                hlDownload.NavigateUrl = ResolveUrl( string.Format( "~/GetFile.ashx?guid={0}&attachment=True", binaryFile.Guid ) );
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the Click event of the lbExport control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbExport_Click( object sender, EventArgs e )
        {
            if ( ValidateSelection() )
            {
                ExportBatches();
            }
        }

        /// <summary>
        /// Handles the Click event of the lbSelectBatches control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbSelectBatches_Click( object sender, EventArgs e )
        {
            var rockContext = new RockContext();
            var batchIds = gBatches.SelectedKeys.Cast<int>().ToList();

            hfBatchIds.Value = string.Join( ",", batchIds.Select( i => i.ToString() ) );


            ValidateSelection( true );
        }

        /// <summary>
        /// Handles the ApplyFilterClick event of the gfBatches control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void gfBatches_ApplyFilterClick( object sender, EventArgs e )
        {
            gfBatches.SetFilterPreference( "Date Range", drpBatchDate.DelimitedValues );
            gfBatches.SetFilterPreference( "Export Date Range", drpExportDate.DelimitedValues );
            gfBatches.SetFilterPreference( "Title", tbTitle.Text );
            gfBatches.SetFilterPreference( "Status", ddlStatus.SelectedValue );
            gfBatches.SetFilterPreference( "Campus", campCampus.SelectedValue );
            gfBatches.SetFilterPreference( "Deposited", ddlDeposited.SelectedValue );

            BindBatchesGrid();
        }

        /// <summary>
        /// Handles the ClearFilterClick event of the gfBatches control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void gfBatches_ClearFilterClick( object sender, EventArgs e )
        {
            gfBatches.DeleteFilterPreferences();
            BindBatchesFilter();
        }

        /// <summary>
        /// Gets the batches display filter value.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        protected void gfBatches_DisplayFilterValue( object sender, GridFilter.DisplayFilterValueArgs e )
        {
            switch ( e.Key )
            {
                case "Date Range":
                    {
                        e.Value = DateRangePicker.FormatDelimitedValues( e.Value );
                        break;
                    }

                case "Export Date Range":
                    {
                        e.Value = DateRangePicker.FormatDelimitedValues( e.Value );
                        break;
                    }

                case "Status":
                    {
                        var status = e.Value.ConvertToEnumOrNull<BatchStatus>();
                        e.Value = status.HasValue ? status.ConvertToString() : string.Empty;
                        break;
                    }

                case "Campus":
                    {
                        var campus = CampusCache.Get( e.Value.AsInteger() );
                        e.Value = campus != null ? campus.Name : string.Empty;
                        break;
                    }

                case "Deposited":
                    {
                        var deposited = e.Value.ConvertToEnumOrNull<IsDeposited>();
                        e.Value = deposited.HasValue ? deposited.ConvertToString() : string.Empty;
                        break;
                    }
            }
        }

        /// <summary>
        /// Handles the GridRebind event of the gBatches control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GridRebindEventArgs"/> instance containing the event data.</param>
        protected void gBatches_GridRebind( object sender, GridRebindEventArgs e )
        {
            BindBatchesGrid();
        }

        protected void gBatches_RowDataBound( object sender, GridViewRowEventArgs e )
        {
            var showTransactionDetails = GetAttributeValue( "ShowTransactionDetailsOnGrid" ).AsBoolean();
            if ( e.Row.RowType != DataControlRowType.DataRow )
            {
                return;
            }

            FinancialBatch financialBatch = e.Row.DataItem as FinancialBatch;
            if ( financialBatch == null )
            {
                return;
            }

            financialBatch.LoadAttributes();

            var lTransactionCount = e.Row.FindControl( "lTransactionCount" ) as Literal;
            if ( lTransactionCount != null )
            {
                if ( showTransactionDetails )
                {
                    lTransactionCount.Text = financialBatch.Transactions.Count().ToString();
                }
            }

            var transactionAmount = financialBatch.ControlAmount;
            var lTransactionAmount = e.Row.FindControl( "lTransactionAmount" ) as Literal;
            if ( lTransactionAmount != null )
            {
                if ( showTransactionDetails )
                {
                    transactionAmount = financialBatch.Transactions.SelectMany( t => t.TransactionDetails ).Sum( d => ( decimal? ) d.Amount ) ?? 0.0M;
                }

                lTransactionAmount.Text = transactionAmount.FormatAsCurrency();
            }

            var lVariance = e.Row.FindControl( "lVariance" ) as Literal;
            if ( lVariance != null )
            {

                decimal variance = transactionAmount - financialBatch.ControlAmount;
                lVariance.Text = string.Format( "<span class='{0}'>{1}</span>"
                        , variance != 0 ? "label label-danger" : ""
                        , variance.FormatAsCurrency() );

            }

            var lStatus = e.Row.FindControl( "lStatus" ) as Literal;
            if ( lStatus != null )
            {
                var statusLabelClass = string.Empty;
                switch ( financialBatch.Status )
                {
                    case BatchStatus.Closed:
                        statusLabelClass = "label label-default";
                        break;
                    case BatchStatus.Open:
                        statusLabelClass = "label label-info";
                        break;
                    case BatchStatus.Pending:
                        statusLabelClass = "label label-warning";
                        break;
                }

                lStatus.Text = string.Format( "<span class='{0}'>{1}</span>"
                        , statusLabelClass
                        , financialBatch.Status.ConvertToString() );
            }

            var lNotes = e.Row.FindControl( "lNotes" ) as Literal;
            if ( lNotes != null )
            {
                var notes = new StringBuilder();
                var unMatchedTransactions = financialBatch.Transactions.Any( t => !t.AuthorizedPersonAliasId.HasValue );
                if ( financialBatch.Status == BatchStatus.Open && unMatchedTransactions )
                {
                    notes.Append( "<span class='label label-warning'>Unmatched Transactions</span><br/>" );
                }

                notes.Append( financialBatch.Note );

                lNotes.Text = notes.ToString();
            }

            var lDeposited = e.Row.FindControl( "lDeposited" ) as Literal;
            if ( lDeposited != null )
            {
                lDeposited.Text = financialBatch.GetAttributeValue( "com.bemaservices.Deposited" ).AsBoolean().ToYesNo();
            }

            var lExportDate = e.Row.FindControl( "lExportDate" ) as Literal;
            if ( lExportDate != null )
            {
                lExportDate.Text = financialBatch.GetAttributeValue( "com.bemaservices.ExportDate" ).AsDateTime().ToShortDateString();
            }

            var lExportFile = e.Row.FindControl( "lExportFile" ) as Literal;
            if ( lExportFile != null )
            {
                var fileGuid = financialBatch.GetAttributeValue( "com.bemaservices.ExportFile" ).AsGuidOrNull();
                if ( fileGuid.HasValue )
                {
                    var binaryFile = new BinaryFileService( new RockContext() ).Get( fileGuid.Value );
                    if ( binaryFile != null )
                    {
                        lExportFile.Text = string.Format( "<a href='/GetFile.ashx?guid={0}' target='_blank'>{1}</a>", binaryFile.Guid, binaryFile.FileName );
                    }
                }
            }
        }


        /// <summary>
        /// Handles the Click event of the lbCancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbCancel_Click( object sender, EventArgs e )
        {
            pnlFixMicr.Visible = false;
            pnlOptions.Visible = false;
            pnlBatches.Visible = true;

            BindBatchesGrid();
        }

        /// <summary>
        /// Handles the Click event of the lbFinished control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbFinished_Click( object sender, EventArgs e )
        {
            NavigateToCurrentPage();
        }

        /// <summary>
        /// Handles the Click event of the lbFixMicr control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbFixMicr_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var transactionService = new FinancialTransactionService( rockContext );

                //Loop through each check transaction and format details
                foreach ( RepeaterItem item in rptMicrDetail.Items )
                {
                    var hfFixMicrId = ( item.FindControl( "hfFixMicrId" ) as HiddenField );
                    var tbRoutingNumber = ( item.FindControl( "tbRoutingNumber" ) as RockTextBox );
                    var tbAccountNumber = ( item.FindControl( "tbAccountNumber" ) as RockTextBox );
                    var tbCheckNumber = ( item.FindControl( "tbCheckNumber" ) as RockTextBox );
                    var hfIsBankCheck = ( item.FindControl( "hfIsBankCheck" ) as HiddenField );
                    var tbAmount = ( item.FindControl( "tbAmount" ) as CurrencyBox );

                    var transaction = transactionService.Get( hfFixMicrId.Value.AsInteger() );
                    var micrText = string.Format( "d{0}d{1}c{2}", tbRoutingNumber.Text, tbAccountNumber.Text, tbCheckNumber.Text );
                    if ( hfIsBankCheck.Value.AsBoolean() )
                    {
                        // if Bank check, write it slightly differently
                        micrText = string.Format( "c{0}c d{1}d{2}c", tbCheckNumber.Text, tbRoutingNumber.Text, tbAccountNumber.Text );
                    }
                    transaction.CheckMicrEncrypted = Rock.Security.Encryption.EncryptString( micrText );
                    transaction.CheckMicrHash = Rock.Security.Encryption.GetSHA1Hash( micrText );

                    rockContext.SaveChanges();
                }

                if ( ValidateSelection() )
                {
                    ShowOptions();
                }
            }

        }

        #endregion

        #region Support Classes


        public class BatchRow
        {
            public int Id { get; set; }
            public DateTime BatchStartDateTime { get; set; }
            public string Name { get; set; }
            public string AccountingSystemCode { get; set; }
            public int TransactionCount { get; set; }
            public decimal TransactionAmount { get; set; }
            public decimal ControlAmount { get; set; }
            public string CampusName { get; set; }
            public BatchStatus Status { get; set; }
            public bool UnMatchedTxns { get; set; }
            public string BatchNote { get; set; }

            public decimal Variance
            {
                get
                {
                    return TransactionAmount - ControlAmount;
                }
            }

            public string StatusText
            {
                get
                {
                    return Status.ConvertToString();
                }
            }


            public string StatusLabelClass
            {
                get
                {
                    switch ( Status )
                    {
                        case BatchStatus.Closed:
                            return "label label-default";
                        case BatchStatus.Open:
                            return "label label-info";
                        case BatchStatus.Pending:
                            return "label label-warning";
                    }

                    return string.Empty;
                }
            }

            public string Notes
            {
                get
                {
                    var notes = new StringBuilder();

                    switch ( Status )
                    {
                        case BatchStatus.Open:
                            {
                                if ( UnMatchedTxns )
                                {
                                    notes.Append( "<span class='label label-warning'>Unmatched Transactions</span><br/>" );
                                }

                                break;
                            }
                    }

                    notes.Append( BatchNote );

                    return notes.ToString();
                }
            }

            public bool Deposited { get; set; }
            public DateTime? ExportDate { get; set; }
            public string ExportFileName { get; set; }
            public string ExportFileUrl { get; set; }
        }

        public class MicrRow
        {
            public int TransactionId { get; set; }

            public string RoutingNumber { get; set; }

            public string AccountNumber { get; set; }

            public string CheckNumber { get; set; }

            public bool IsBankCheck { get; set; }

            public decimal Amount { get; set; }

            public string ImageUrl { get; set; }

            public bool? IsValid { get; set; }

            public string IsValidMessage { get; set; }
        }

        #endregion
    }
}
