        [HttpPost]
        public async Task<IActionResult> UpdatePUSupplierDetail([FromForm] IFormCollection form)
        {
            MessageSelectionNewSupplier msg = new MessageSelectionNewSupplier();
            var requestJson = form["Request"];
            var annualRevenueJson = form["AnnualRevenue"];
            var paymentAccountJson = form["PaymentAccount"];
            var responsibleContactJson = form["ResponsibleContact"];
            var factoryJson = form["Factory"];
            var performanceRankingJson = form["PerformanceRankingAndEvaluation"];
            var actualContractExecutionJson = form["ActualContractExecution"];
            var revenueAndPerformanceJson = form["RevenueAndPerformance"];
            var investigationResultsJson = form["InvestigationResults"];
            var documentDistributionHistoryJson = form["DocumentDistributionHistory"];
            var filesToDeleteJson = form["FilesToDelete"];
            var additionalInvestigationRowsJson = form["AdditionalInvestigationRows"];
            var additionalInvestigationRows = string.IsNullOrEmpty(additionalInvestigationRowsJson)
                ? new List<PUAdditionalInvestigationRow>()
                : JsonConvert.DeserializeObject<List<PUAdditionalInvestigationRow>>(additionalInvestigationRowsJson);
            var notesJson = form["Notes"];
            var sendInfoJson = form["SendInfo"];
            bool headOfficeSapSynced = string.Equals(form["HeadOfficeSapSynced"], "true", StringComparison.OrdinalIgnoreCase);
            bool hasFactory = string.Equals(form["HasFactory"], "true", StringComparison.OrdinalIgnoreCase);
            bool transactionOfficeSapSynced = string.Equals(form["TransactionOfficeSapSynced"], "true", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(requestJson))
            {
                msg.result = false;
                msg.message = "Request is required";
                return Ok(msg);
            }
            var s = JsonConvert.DeserializeObject<PUSupplierData>(requestJson);
            if (string.IsNullOrEmpty(s?.RequestCode))
            {
                msg.result = false;
                msg.message = "RequestCode is required";
                return Ok(msg);
            }
            var annualRevenue = string.IsNullOrEmpty(annualRevenueJson)
                ? new List<PUAnnualRevenue>()
                : JsonConvert.DeserializeObject<List<PUAnnualRevenue>>(annualRevenueJson);
            var paymentAccount = string.IsNullOrEmpty(paymentAccountJson)
                ? new List<PUPaymentAccount>()
                : JsonConvert.DeserializeObject<List<PUPaymentAccount>>(paymentAccountJson);
            var responsibleContact = string.IsNullOrEmpty(responsibleContactJson)
                ? new List<PUResponsibleContact>()
                : JsonConvert.DeserializeObject<List<PUResponsibleContact>>(responsibleContactJson);
            var factory = string.IsNullOrEmpty(factoryJson)
                ? new List<PUFactory>()
                : JsonConvert.DeserializeObject<List<PUFactory>>(factoryJson);
            var performanceRanking = string.IsNullOrEmpty(performanceRankingJson)
                ? new List<PUPerformanceRankingEvaluation>()
                : JsonConvert.DeserializeObject<List<PUPerformanceRankingEvaluation>>(performanceRankingJson);
            var actualContractExecution = string.IsNullOrEmpty(actualContractExecutionJson)
                ? new List<PUActualContractExecution>()
                : JsonConvert.DeserializeObject<List<PUActualContractExecution>>(actualContractExecutionJson);
            var revenuePerformance = string.IsNullOrEmpty(revenueAndPerformanceJson)
                ? new List<PURevenuePerformance>()
                : JsonConvert.DeserializeObject<List<PURevenuePerformance>>(revenueAndPerformanceJson);
            var documentDistributionHistory = string.IsNullOrEmpty(documentDistributionHistoryJson)
                ? new List<PUDocumentDistributionHistory>()
                : JsonConvert.DeserializeObject<List<PUDocumentDistributionHistory>>(documentDistributionHistoryJson);
            PUInvestigationResults investigationResults = string.IsNullOrEmpty(investigationResultsJson)
                ? new PUInvestigationResults()
                : JsonConvert.DeserializeObject<PUInvestigationResults>(investigationResultsJson);
            var notes = string.IsNullOrEmpty(notesJson)
                    ? new List<PUNote>()
                    : JsonConvert.DeserializeObject<List<PUNote>>(notesJson);
            var syncedContactIds = responsibleContact
                .Where(c => c.SapSynced)
                .Select(c => c.Id)
                .ToHashSet();
            var syncedBankIds = paymentAccount
                .Where(b => b.SapSynced)
                .Select(b => b.Id)
                .ToHashSet();
            using var conn = new MySqlConnection(ConnectDB.connectString);
            await conn.OpenAsync();
            using var trans = await conn.BeginTransactionAsync();
            try
            {
                string query;
                string currentUser = User.FindFirstValue("UserName");
                var oldInfoDt = ConnectDB.ExecuteQuery(
                    $@"SELECT * FROM pu_supplier_detail_info WHERE RequestCode = '{s.RequestCode}'");
                System.Data.DataRow oldInfoRow = oldInfoDt.Rows.Count > 0 ? oldInfoDt.Rows[0] : null;
                var allTrackedFields = new List<PUTrackedField>();
                string ParseIntOrNull(string raw)
                {
                    if (string.IsNullOrWhiteSpace(raw)) return "NULL";
                    return int.TryParse(raw, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var v)
                        ? v.ToString(CultureInfo.InvariantCulture)
                        : "NULL";
                }
                double? authorizedCapital = double.TryParse((s?.AuthorizedCapital ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var AuthorizedCapital) ? AuthorizedCapital : (double?)null;
                double? dependencySubcontractor = double.TryParse((s?.DependencySubcontractor ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var depSub) ? depSub : (double?)0;
                double? numberSubcontractor = double.TryParse((s?.NumberSubcontractor ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var numberSub) ? numberSub : (double?)0;
                double? investigationScoreDocument = double.TryParse((s?.PUInvestigationScoreDocument ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var investScore) ? investScore : (double?)0;
                double? investigationScorePlanned = double.TryParse((s?.PUInvestigationScorePlanned ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var investScorePlan) ? investScorePlan : (double?)0;
                query = $@"
        INSERT INTO pu_supplier_detail_info (
            RequestCode, SupplierCode, StatusSubmit,
            CompanyName, AdditionalCompanyName, CompanyNameEN, AdditionalCompanyNameEN,
            TaxCode, EstablishmentDate, AuthorizedCapital, AuthorizedCapitalCurrency,
            HeadOfficeWebsite, Representative, RepresentativePosition, RepresentativeYearOfBirth,
            NumberEmployees, NumberEmployeeTemporary, BusinessType, TypeCompany, CountryOwnership,
            ManufacturingEnterprise, LegalForm, SettlementMonth, Listing,
            CertificateISO9001, CertificateISO14001, CertificateOthers,
            HeadOfficeAddressDetail, HeadOfficeHouseNumber, HeadOfficeStreet, HeadOfficeDistrict,
            HeadOfficeProvinceCity, HeadOfficeCountry, HeadOfficePhoneNumber,
            TransactionOfficeAddressDetail, TransactionOfficeHouseNumber, TransactionOfficeStreet,
            TransactionOfficeDistrict, TransactionOfficeProvinceCity, TransactionOfficeCountry,
            TransactionOfficePhoneNumber, TransactionOfficeGoogleMap,
            CompanyNameMainCustomer1, DependencyMainCustomer1, NumberOfTradingYearsMainCustomer1,
            CompanyNameMainCustomer2, DependencyMainCustomer2, NumberOfTradingYearsMainCustomer2,
            CompanyNameMainCustomer3, DependencyMainCustomer3, NumberOfTradingYearsMainCustomer3,
            CompanyNameSupplier1, DependencySupplier1, CompanyNameSupplier2, DependencySupplier2,
            NumberSubcontractor, DependencySubcontractor,
            DetailedIndustryClassification, TypeOfPurchasedGoods, PurchaseClassification,
            RepresentativeItemName, RepresentativeProductCode, MainMaker, PUManageResponsiblePerson,
            TradeName, PUManageSupplierClassification, Section, Requester, ReleasePOViaSAP,
            PurposeOfUse, SpecialSelectionTarget, InitialRegistrationDate, LastTransaction,
            Industry, VND, USD, Incoterm, InvestigationMethod,
            InvestigationDateDocument, InvestigationScoreDocument, InvestigationEvaluateDocument,
            InvestigationDatePlanned, InvestigationScorePlanned, InvestigationEvaluatePlanned, SetUp,
            QAPaymentTerms, Creator,
            BorrowedAssetClassification, PUManageUsingDepartment1, PUManageUsingDepartment2, HasFactory,
            UserCreated, CreateAtTime, UserUpdated, UpdateAtTime
        ) VALUES (
            '{s.RequestCode}', '{s.SupplierCode}', '{s.Status}',
            '{s.CompanyName}', '{s.AdditionalCompanyName}', '{s.CompanyNameEN}', '{s.AdditionalCompanyNameEN}',
            '{s.TaxCode}', '{s.EstablishmentDate}',
            {(authorizedCapital.HasValue ? authorizedCapital.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},
            '{s.AuthorizedCapitalCurrency}',
            '{s.Website}', '{s.LegalRepresentativeName}', '{s.PositionRole}', '{s.YearOfBirth}',
            {ParseIntOrNull(s.OfficialEmp)}, {ParseIntOrNull(s.TemporaryEmp)}, '{s.BusinessType}', '{s.TypeCompany}', '{s.CountryOwnership}',            '{s.ManufacturingEnterprise}', '{s.LegalForm}', '{s.SettlementMonth}', '{s.Listing}',
            {ToBitFlag(s.CertificateISO9001)}, {ToBitFlag(s.CertificateISO14001)}, '{s.CertificateOthers}',
            '{s.HeadOfficeAddressDetail}', '{s.HeadOfficeHouseNumber}', '{s.HeadOfficeStreet}', '{s.HeadOfficeDistrict}',
            '{s.HeadOfficeProvinceCity}', '{s.HeadOfficeCountry}', '{s.HeadOfficePhoneNumber}',
            '{s.TransactionOfficeAddressDetail}', '{s.TransactionOfficeHouseNumber}', '{s.TransactionOfficeStreet}',
            '{s.TransactionOfficeDistrict}', '{s.TransactionOfficeProvinceCity}', '{s.TransactionOfficeCountry}',
            '{s.TransactionOfficePhoneNumber}', '{s.TransactionOfficeGoogleMap}',
            '{s.CompanyNameMainCustomer1}', '{s.DependencyMainCustomer1}', '{s.NumberOfTradingYearsMainCustomer1}',
            '{s.CompanyNameMainCustomer2}', '{s.DependencyMainCustomer2}', '{s.NumberOfTradingYearsMainCustomer2}',
            '{s.CompanyNameMainCustomer3}', '{s.DependencyMainCustomer3}', '{s.NumberOfTradingYearsMainCustomer3}',
            '{s.CompanyNameSupplier1}', {(s.DependencySupplier1 == null || s.DependencySupplier1 == "" ? "NULL" : $"{s.DependencySupplier1}")},
            '{s.CompanyNameSupplier2}', {(s.DependencySupplier2 == null || s.DependencySupplier2 == "" ? "NULL" : $"{s.DependencySupplier2}")},
            {numberSubcontractor}, {dependencySubcontractor},
            '{s.DetailedIndustryClassification}', '{s.TypeOfPurchasedGoods}', '{s.PurchaseClassification}',
            '{s.RepresentativeItemName}', '{s.RepresentativeProductCode}', '{s.MainMaker}', '{s.PUResponsiblePerson}',
            '{s.TradeName}', '{s.PUManageSupplierClassification}', '{s.Section}', '{s.Requester}', '{s.ReleasePOViaSAP}',
            '{s.SelectionRequestReason}', '{s.SpecialSelectionTarget}',
            {(string.IsNullOrEmpty(s.InitialRegistrationDate) ? "NULL" : $"'{s.InitialRegistrationDate}'")},
            '{s.LastTransaction}',
            '{s.Industry}', {ToBitFlag(s.VND)}, {ToBitFlag(s.USD)}, '{s.Incoterm}', '{s.InvestigationMethod}',
            '{s.PUInvestigationDateDocument}', {investigationScoreDocument}, '{s.PUInvestigationEvaluateDocument}',
            '{s.PUInvestigationDatePlanned}', {investigationScorePlanned}, '{s.PUInvestigationEvaluatePlanned}', '{s.PurchasingDepartment}',
            '{s.QAPaymentTerms}', '{s.PurchasingManagementDepartment}',
            '{s.BorrowedAssetClassification}', '{s.PUManageUsingDepartment1}', '{s.PUManageUsingDepartment2}',{(hasFactory ? 1 : 0)},
            '{currentUser}', NOW(), '{currentUser}', NOW()
        )
        ON DUPLICATE KEY UPDATE
            SupplierCode = VALUES(SupplierCode),
            StatusSubmit = VALUES(StatusSubmit),
            CompanyName = VALUES(CompanyName),
            AdditionalCompanyName = VALUES(AdditionalCompanyName),
            CompanyNameEN = VALUES(CompanyNameEN),
            AdditionalCompanyNameEN = VALUES(AdditionalCompanyNameEN),
            TaxCode = VALUES(TaxCode),
            EstablishmentDate = VALUES(EstablishmentDate),
            AuthorizedCapital = VALUES(AuthorizedCapital),
            AuthorizedCapitalCurrency = VALUES(AuthorizedCapitalCurrency),
            HeadOfficeWebsite = VALUES(HeadOfficeWebsite),
            Representative = VALUES(Representative),
            RepresentativePosition = VALUES(RepresentativePosition),
            RepresentativeYearOfBirth = VALUES(RepresentativeYearOfBirth),
            NumberEmployees = VALUES(NumberEmployees),
            NumberEmployeeTemporary = VALUES(NumberEmployeeTemporary),
            BusinessType = VALUES(BusinessType),
            TypeCompany = VALUES(TypeCompany),
            CountryOwnership = VALUES(CountryOwnership),
            ManufacturingEnterprise = VALUES(ManufacturingEnterprise),
            LegalForm = VALUES(LegalForm),
            SettlementMonth = VALUES(SettlementMonth),
            Listing = VALUES(Listing),
            CertificateISO9001 = VALUES(CertificateISO9001),
            CertificateISO14001 = VALUES(CertificateISO14001),
            CertificateOthers = VALUES(CertificateOthers),
            HeadOfficeAddressDetail = VALUES(HeadOfficeAddressDetail),
            HeadOfficeHouseNumber = VALUES(HeadOfficeHouseNumber),
            HeadOfficeStreet = VALUES(HeadOfficeStreet),
            HeadOfficeDistrict = VALUES(HeadOfficeDistrict),
            HeadOfficeProvinceCity = VALUES(HeadOfficeProvinceCity),
            HeadOfficeCountry = VALUES(HeadOfficeCountry),
            HeadOfficePhoneNumber = VALUES(HeadOfficePhoneNumber),
            TransactionOfficeAddressDetail = VALUES(TransactionOfficeAddressDetail),
            TransactionOfficeHouseNumber = VALUES(TransactionOfficeHouseNumber),
            TransactionOfficeStreet = VALUES(TransactionOfficeStreet),
            TransactionOfficeDistrict = VALUES(TransactionOfficeDistrict),
            TransactionOfficeProvinceCity = VALUES(TransactionOfficeProvinceCity),
            TransactionOfficeCountry = VALUES(TransactionOfficeCountry),
            TransactionOfficePhoneNumber = VALUES(TransactionOfficePhoneNumber),
            TransactionOfficeGoogleMap = VALUES(TransactionOfficeGoogleMap),
            CompanyNameMainCustomer1 = VALUES(CompanyNameMainCustomer1),
            DependencyMainCustomer1 = VALUES(DependencyMainCustomer1),
            NumberOfTradingYearsMainCustomer1 = VALUES(NumberOfTradingYearsMainCustomer1),
            CompanyNameMainCustomer2 = VALUES(CompanyNameMainCustomer2),
            DependencyMainCustomer2 = VALUES(DependencyMainCustomer2),
            NumberOfTradingYearsMainCustomer2 = VALUES(NumberOfTradingYearsMainCustomer2),
            CompanyNameMainCustomer3 = VALUES(CompanyNameMainCustomer3),
            DependencyMainCustomer3 = VALUES(DependencyMainCustomer3),
            NumberOfTradingYearsMainCustomer3 = VALUES(NumberOfTradingYearsMainCustomer3),
            CompanyNameSupplier1 = VALUES(CompanyNameSupplier1),
            DependencySupplier1 = VALUES(DependencySupplier1),
            CompanyNameSupplier2 = VALUES(CompanyNameSupplier2),
            DependencySupplier2 = VALUES(DependencySupplier2),
            NumberSubcontractor = VALUES(NumberSubcontractor),
            DependencySubcontractor = VALUES(DependencySubcontractor),
            DetailedIndustryClassification = VALUES(DetailedIndustryClassification),
            TypeOfPurchasedGoods = VALUES(TypeOfPurchasedGoods),
            PurchaseClassification = VALUES(PurchaseClassification),
            RepresentativeItemName = VALUES(RepresentativeItemName),
            RepresentativeProductCode = VALUES(RepresentativeProductCode),
            MainMaker = VALUES(MainMaker),
            PUManageResponsiblePerson = VALUES(PUManageResponsiblePerson),
            TradeName = VALUES(TradeName),
            PUManageSupplierClassification = VALUES(PUManageSupplierClassification),
            Section = VALUES(Section),
            Requester = VALUES(Requester),
            ReleasePOViaSAP = VALUES(ReleasePOViaSAP),
            PurposeOfUse = VALUES(PurposeOfUse),
            SpecialSelectionTarget = VALUES(SpecialSelectionTarget),
            InitialRegistrationDate = VALUES(InitialRegistrationDate),
            LastTransaction = VALUES(LastTransaction),
            Industry = VALUES(Industry),
            VND = VALUES(VND),
            USD = VALUES(USD),
            Incoterm = VALUES(Incoterm),
            InvestigationMethod = VALUES(InvestigationMethod),
            InvestigationDateDocument = VALUES(InvestigationDateDocument),
            InvestigationScoreDocument = VALUES(InvestigationScoreDocument),
            InvestigationEvaluateDocument = VALUES(InvestigationEvaluateDocument),
            InvestigationDatePlanned = VALUES(InvestigationDatePlanned),
            InvestigationScorePlanned = VALUES(InvestigationScorePlanned),
            InvestigationEvaluatePlanned = VALUES(InvestigationEvaluatePlanned),
            SetUp = VALUES(SetUp),
            QAPaymentTerms = VALUES(QAPaymentTerms),
            Creator = VALUES(Creator),
            BorrowedAssetClassification = VALUES(BorrowedAssetClassification),
            PUManageUsingDepartment1 = VALUES(PUManageUsingDepartment1),
            PUManageUsingDepartment2 = VALUES(PUManageUsingDepartment2),
            HasFactory = VALUES(HasFactory),
            UserUpdated = '{currentUser}',
            UpdateAtTime = NOW()";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                query = $@"DELETE FROM pu_supplier_detail_contact WHERE RequestCode = '{s.RequestCode}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                int seqContact = 1;
                foreach (var item in responsibleContact)
                {
                    query = $@"
            INSERT INTO pu_supplier_detail_contact
            (
                Id,
                RequestCode,
                SeqNo,
                PersonTitle,
                LastName,
                FullName,
                Email,
                Phone,
                Type,
                CreateAtTime,
                UserCreated,
                UpdateAtTime,
                UserUpdated
            )
            VALUES
            (
                '{item.Id}',
                '{s.RequestCode}',
                {seqContact},
                '{item.PersonTitle}',
                '{item.LastName}',
                '{item.FullName}',
                '{item.Email}',
                '{item.Phone}',
                '{item.Type}',
                NOW(),
                '{currentUser}',
                NOW(),
                '{currentUser}'
            )
            ON DUPLICATE KEY UPDATE
                RequestCode = VALUES(RequestCode),
                SeqNo = VALUES(SeqNo),
                PersonTitle = VALUES(PersonTitle),
                LastName = VALUES(LastName),
                FullName = VALUES(FullName),
                Email = VALUES(Email),
                Phone = VALUES(Phone),
                Type = VALUES(Type),
                UpdateAtTime = NOW(),
                UserUpdated = '{currentUser}';
            ";
                    await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    seqContact++;
                }
                query = $@"DELETE FROM pu_supplier_detail_account_bank WHERE RequestCode = '{s.RequestCode}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                int seqAccount = 1;
                foreach (var item in paymentAccount)
                {
                    query = $@"INSERT INTO pu_supplier_detail_account_bank
            (Id, SeqNo, RequestCode, AccountType, AccountNumber, AccountName, BankName, BranchAccountName, BranchAccountAddress,
             BranchAccountSWIFTCode, CitadCode, ReasonTransactionByUSD, ObjectTransactionByUS, IdAccountBank,
             CreateAtTime, UserCreated, UpdateAtTime, UserUpdated)
           VALUES (
            '{item.Id}', {seqAccount}, '{s.RequestCode}',
            '{item.AccountType}', '{item.AccountNumber}', '{item.AccountName}', '{item.BankName}',
            '{item.BranchAccountName}', '{item.BranchAccountAddress}', '{item.BranchAccountSWIFTCode}',
            '{item.CitadCode}', '{item.ReasonTransactionByUSD}', '{item.ObjectTransactionByUS}', '{item.IdAccountBank}',
            NOW(), '{currentUser}', NOW(), '{currentUser}'
           )";
                    await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    seqAccount++;
                }
                query = $@"DELETE FROM pu_supplier_detail_factory WHERE RequestCode = '{s.RequestCode}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                int seqFactory = 1;
                foreach (var item in factory)
                {
                    double? depMainCustomerOne = double.TryParse((item?.FactoryDependencyMainCustomerOne ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var d1) ? d1 : (double?)null;
                    double? depMainCustomerTwo = double.TryParse((item?.FactoryDependencyMainCustomerTwo ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var d2) ? d2 : (double?)null;
                    double? depMainCustomerThree = double.TryParse((item?.FactoryDependencyMainCustomerThree ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var d3) ? d3 : (double?)null;
                    double? depSupplierOne = double.TryParse((item?.FactoryDependencySupplierOne ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var d4) ? d4 : (double?)null;
                    double? depSupplierTwo = double.TryParse((item?.FactoryDependencySupplierTwo ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var d5) ? d5 : (double?)null;
                    double? yearsOne = double.TryParse((item?.FactoryNumberOfTradingYearsMainCustomerOne ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var y1) ? y1 : (double?)null;
                    double? yearsTwo = double.TryParse((item?.FactoryNumberOfTradingYearsMainCustomerTwo ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var y2) ? y2 : (double?)null;
                    double? yearsThree = double.TryParse((item?.FactoryNumberOfTradingYearsMainCustomerThree ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var y3) ? y3 : (double?)null;
                    query = $@"INSERT INTO pu_supplier_detail_factory
                (Id, RequestCode, SeqNo, CompanyName, TaxCode, Major, ReasonUse, ContentEntrust, TypicalProductCode, MoldLend,
                 Representative, RepresentativePosition, RepresentativeYearOfBirth, NumberEmployeeOffical, NumberEmployeeTemporary,
                 HeadOfficeFactoryAddressDetail, HeadOfficeFactoryHouseNumber, HeadOfficeFactoryStreet, HeadOfficeFactoryPhoneNumber,
                 HeadOfficeFactoryWebsite, HeadOfficeFactoryDistrict, HeadOfficeFactoryProvinceCity, HeadOfficeFactoryCountry,
                 FactoryAddressDetail, FactoryHouseNumber, FactoryStreet, FactoryPhoneNumber, FactoryGoogleMap, FactoryDistrict,
                 FactoryProvinceCity, FactoryCountry, FactoryTransactionBank, FactoryMainCustomerOne, FactoryDependencyMainCustomerOne,
                 FactoryNumberOfTradingYearsMainCustomerOne, FactoryMainCustomerTwo, FactoryDependencyMainCustomerTwo,
                 FactoryNumberOfTradingYearsMainCustomerTwo, FactoryMainCustomerThree, FactoryDependencyMainCustomerThree,
                 FactoryNumberOfTradingYearsMainCustomerThree, FactorySupplierOne, FactoryDependencySupplierOne, FactorySupplierTwo,
                 FactoryDependencySupplierTwo, FactoryTradingCompanyName, FactoryTradingCompanyAddress,
                 CreateAtTime, UserCreated, UpdateAtTime, UserUpdated)
               VALUES (
                '{item.Id}', '{s.RequestCode}', {seqFactory},
                '{item.CompanyName}', '{item.TaxCode}', '{item.Major}', '{item.ReasonUse}', '{item.ContentEntrust}',
                '{item.TypicalProductCode}', '{item.MoldLend}', '{item.Representative}', '{item.RepresentativePosition}',
                '{item.RepresentativeYearOfBirth}',
                {ParseIntOrNull(item.NumberEmployeeOffical)},
                {ParseIntOrNull(item.NumberEmployeeTemporary)},
                '{item.HeadOfficeFactoryAddressDetail}', '{item.HeadOfficeFactoryHouseNumber}', '{item.HeadOfficeFactoryStreet}',
                '{item.HeadOfficeFactoryPhoneNumber}', '{item.HeadOfficeFactoryWebsite}', '{item.HeadOfficeFactoryDistrict}',
                '{item.HeadOfficeFactoryProvinceCity}', '{item.HeadOfficeFactoryCountry}',
                '{item.FactoryAddressDetail}', '{item.FactoryHouseNumber}', '{item.FactoryStreet}', '{item.FactoryPhoneNumber}',
                '{item.FactoryGoogleMap}', '{item.FactoryDistrict}', '{item.FactoryProvinceCity}', '{item.FactoryCountry}',
                '{item.FactoryTransactionBank}', '{item.FactoryMainCustomerOne}',
                {(depMainCustomerOne.HasValue ? depMainCustomerOne.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},
                {(yearsOne.HasValue ? yearsOne.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},
                '{item.FactoryMainCustomerTwo}',
                {(depMainCustomerTwo.HasValue ? depMainCustomerTwo.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},
                {(yearsTwo.HasValue ? yearsTwo.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},
                '{item.FactoryMainCustomerThree}',
                {(depMainCustomerThree.HasValue ? depMainCustomerThree.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},
                {(yearsThree.HasValue ? yearsThree.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},
                '{item.FactorySupplierOne}',
                {(depSupplierOne.HasValue ? depSupplierOne.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},
                '{item.FactorySupplierTwo}',
                {(depSupplierTwo.HasValue ? depSupplierTwo.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},
                '{item.FactoryTradingCompanyName}', '{item.FactoryTradingCompanyAddress}',
                NOW(), '{currentUser}', NOW(), '{currentUser}'
               )";
                    await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    seqFactory++;
                }
                query = $@"DELETE FROM pu_supplier_detail_revenue WHERE RequestCode = '{s.RequestCode}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                int seqRevenue = 1;
                foreach (var item in annualRevenue)
                {
                    double? revenue = double.TryParse((item?.Revenue ?? "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var rev) ? rev : (double?)null;
                    query = $@"INSERT INTO pu_supplier_detail_revenue
                (Id, RequestCode, SeqNo, Factory, FiscalYear, Revenue, RevenueUnit, Type,
                 CreateAtTime, UserCreated, UpdateAtTime, UserUpdated)
               VALUES (
                '{item.Id}', '{s.RequestCode}', {seqRevenue},
                '{item.Factory}', '{item.FiscalYear}',
                {(revenue.HasValue ? revenue.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},
                '{item.RevenueUnit}', '{item.Type}',
                NOW(), '{currentUser}', NOW(), '{currentUser}'
               )";
                    await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    seqRevenue++;
                }
                query = $@"DELETE FROM pu_supplier_detail_performance_ranking WHERE RequestCode = '{s.RequestCode}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                int seqPerf = 1;
                foreach (var item in performanceRanking)
                {
                    query = $@"INSERT INTO pu_supplier_detail_performance_ranking
            (Id, RequestCode, SeqNo, SupplierRanking, DateExecutionEvaluateSupplier,
             ComprehensiveEvaluationRankingPoint, ComprehensiveEvaluationRankingPoint1,
             DateExecutionEvaluateSummary, SupplierInput,
             CreateAtTime, UserCreated, UpdateAtTime, UserUpdated)
           VALUES (
            '{item.Id}', '{s.RequestCode}', {seqPerf},
            '{item.SupplierRanking}',
            {(string.IsNullOrEmpty(item.DateExecutionEvaluateSupplier) ? "NULL" : $"'{item.DateExecutionEvaluateSupplier}'")},
            '{item.ComprehensiveEvaluationRankingPoint}', '{item.ComprehensiveEvaluationRankingPoint1}',
            {(string.IsNullOrEmpty(item.DateExecutionEvaluateSummary) ? "NULL" : $"'{item.DateExecutionEvaluateSummary}'")},
            '{item.SupplierInput}',
            NOW(), '{currentUser}', NOW(), '{currentUser}'
           )";
                    await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    seqPerf++;
                }
                query = $@"DELETE FROM pu_supplier_detail_actual_contract WHERE RequestCode = '{s.RequestCode}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                int seqContract = 1;
                foreach (var item in actualContractExecution)
                {
                    query = $@"INSERT INTO pu_supplier_detail_actual_contract
             (Id, RequestCode, SeqNo, Type, Rev, DateExecutionEvaluateSupplier, Appendices, ContractType, PUManageUsingDepartment1, SupplierInput,
              CreateAtTime, UserCreated, UpdateAtTime, UserUpdated)
            VALUES (
             '{item.Id}', '{s.RequestCode}', {seqContract},
             '{item.Type}', '{item.Rev}',
             {(string.IsNullOrEmpty(item.DateExecutionEvaluateSupplier) ? "NULL" : $"'{item.DateExecutionEvaluateSupplier}'")},
             '{item.Appendices}', '{item.ContractType}', '{item.PUManageUsingDepartment1}', '{item.SupplierInput}',
             NOW(), '{currentUser}', NOW(), '{currentUser}'
            )";
                    await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    seqContract++;
                }
                query = $@"DELETE FROM pu_supplier_detail_revenue_performance WHERE RequestCode = '{s.RequestCode}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                int seqRevPerf = 1;
                foreach (var item in revenuePerformance)
                {
                    double? rpRevenue = double.TryParse(item?.Revenue?.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var rv) ? rv : (double?)null;
                    double? rpTotalTx = double.TryParse(item?.TotalTransaction?.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var tt) ? tt : (double?)null;
                    double? rpDirectTx = double.TryParse(item?.DirectTransaction?.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var dtx) ? dtx : (double?)null;
                    double? rpSmcRatio = double.TryParse(item?.SMCRevenueRatio?.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var sr) ? sr : (double?)null;
                    query = $@"INSERT INTO pu_supplier_detail_revenue_performance
            (Id, RequestCode, SeqNo, Type, FiscalYear, Revenue, RevenueCurrency, TotalTransaction, TotalTransactionCurrency,
             DirectTransaction, DirectTransactionCurrency, SMCRevenueRatio, SupplierInput,
             CreateAtTime, UserCreated, UpdateAtTime, UserUpdated)
           VALUES (
            '{item.Id}', '{s.RequestCode}', {seqRevPerf},
            '{item.Type}', '{item.Year}',
            {(rpRevenue.HasValue ? rpRevenue.Value.ToString(CultureInfo.InvariantCulture) : "NULL")}, '{item.RevenueCurrency}',
            {(rpTotalTx.HasValue ? rpTotalTx.Value.ToString(CultureInfo.InvariantCulture) : "NULL")}, '{item.TotalTransactionCurrency}',
            {(rpDirectTx.HasValue ? rpDirectTx.Value.ToString(CultureInfo.InvariantCulture) : "NULL")}, '{item.DirectTransactionCurrency}',
            {(rpSmcRatio.HasValue ? rpSmcRatio.Value.ToString(CultureInfo.InvariantCulture) : "NULL")},
            '{item.SupplierInput}',
            NOW(), '{currentUser}', NOW(), '{currentUser}'
           )";
                    await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    seqRevPerf++;
                }
                // =============== Bước 9: Investigation Results (3 section cố định) ===============
                query = $@"DELETE FROM pu_supplier_detail_investigation_result WHERE RequestCode = '{s.RequestCode}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                var investigationRows = new (string SectionKey, string Type, PUInvestigationResultItem Item)[]
                {
            ("purchasing", "document", investigationResults?.purchasing?.document),
            ("purchasing", "onsite",   investigationResults?.purchasing?.onsite),
            ("quality",    "document", investigationResults?.quality?.document),
            ("quality",    "onsite",   investigationResults?.quality?.onsite),
            ("other",      "document", investigationResults?.other?.document),
            ("other",      "onsite",   investigationResults?.other?.onsite),
                };
                foreach (var row in investigationRows)
                {
                    if (row.Item == null) continue;
                    if (string.IsNullOrEmpty(row.Item.date) && string.IsNullOrEmpty(row.Item.resultPoint) && string.IsNullOrEmpty(row.Item.resultText))
                        continue;
                    // SỬA: RowId để NULL cho 3 section cố định (giữ nguyên hành vi cũ, không đổi)
                    query = $@"INSERT INTO pu_supplier_detail_investigation_result
                (RequestCode, RowId, SectionKey, InvestigationType, CustomInvestigationType, InvestigationDate, ResultPoint, ResultText, Note,
                 CreateAtTime, UserCreated, UpdateAtTime, UserUpdated)
               VALUES (
                '{s.RequestCode}', NULL, '{row.SectionKey}', '{row.Type}', NULL,
                {(string.IsNullOrEmpty(row.Item.date) ? "NULL" : $"'{row.Item.date}'")},
                '{row.Item.resultPoint}', '{row.Item.resultText}', NULL,
                NOW(), '{currentUser}', NOW(), '{currentUser}'
               )";
                    await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                }

                // =============== Bước 9b (ĐÃ SỬA BUG): Lưu các hàng Investigation Results được thêm động ===============
                // BUG GỐC: UNIQUE KEY của bảng pu_supplier_detail_investigation_result trước đây chỉ là
                // (RequestCode, SectionKey, InvestigationType) — không có RowId. Vì vậy khi thêm 2+ dòng
                // cùng loại (VD 2 dòng "purchasing"), cả 2 dòng đều sinh ra cùng SectionKey='purchasing' +
                // InvestigationType='document' (hoặc 'onsite') -> INSERT thứ 2 đụng UNIQUE KEY với INSERT
                // thứ 1 -> do có "ON DUPLICATE KEY UPDATE" nên MySQL âm thầm UPDATE đè lên dòng đầu thay vì
                // tạo dòng mới -> cuối cùng chỉ còn 1 dòng/loại trong DB dù FE thêm bao nhiêu dòng.
                //
                // CÁCH SỬA:
                // 1. Đổi UNIQUE KEY của bảng thành (RequestCode, RowId, SectionKey, InvestigationType) - xem
                //    script ALTER TABLE gửi kèm bên dưới (RowId NULL của 3 section cố định ở Bước 9 không bị
                //    ảnh hưởng vì MySQL coi mỗi giá trị NULL trong unique index là khác nhau).
                // 2. Bỏ "ON DUPLICATE KEY UPDATE" ở đây: vì toàn bộ dòng của RequestCode này đã bị DELETE
                //    ngay phía trên (đầu Bước 9), nên chỉ cần INSERT thuần, không cần upsert nữa.
                foreach (var addRow in additionalInvestigationRows)
                {
                    if (addRow == null || string.IsNullOrEmpty(addRow.Type)) continue;

                    // Luôn sinh RowId thật sự duy nhất cho mỗi dòng động (không tái sử dụng Id do FE random
                    // sinh ra để tránh trùng nếu FE lỡ sinh trùng), đảm bảo mỗi dòng là 1 bản ghi riêng biệt.
                    string rowId = string.IsNullOrEmpty(addRow.Id) ? Guid.NewGuid().ToString("N") : addRow.Id;
                    string noteEscaped = (addRow.Note ?? "").Replace("'", "''");
                    string customTypeEscaped = (addRow.InvestigationType ?? "").Replace("'", "''");

                    var cells = new (string SubType, PUInvestigationResultItem Item)[]
                    {
                ("document", addRow.document ?? new PUInvestigationResultItem()),
                ("onsite",   addRow.onsite   ?? new PUInvestigationResultItem()),
                    };

                    foreach (var cell in cells)
                    {
                        // ĐÃ BỎ "ON DUPLICATE KEY UPDATE": chỉ INSERT thuần vì dữ liệu cũ đã bị xoá sạch ở trên.
                        query = $@"INSERT INTO pu_supplier_detail_investigation_result
                (RequestCode, RowId, SectionKey, InvestigationType, CustomInvestigationType, InvestigationDate, ResultPoint, ResultText, Note,
                 CreateAtTime, UserCreated, UpdateAtTime, UserUpdated)
               VALUES (
                '{s.RequestCode}', '{rowId}', '{addRow.Type}', '{cell.SubType}', '{customTypeEscaped}',
                {(string.IsNullOrEmpty(cell.Item.date) ? "NULL" : $"'{cell.Item.date}'")},
                '{(cell.Item.resultPoint ?? "").Replace("'", "''")}', '{(cell.Item.resultText ?? "").Replace("'", "''")}', '{noteEscaped}',
                NOW(), '{currentUser}', NOW(), '{currentUser}'
               )";
                        await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    }
                }
                query = $@"DELETE FROM pu_supplier_detail_notes WHERE RequestCode = '{s.RequestCode}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                int seqNote = 1;
                foreach (var item in notes)
                {
                    if (string.IsNullOrWhiteSpace(item.Content)) continue;
                    string rowId2 = string.IsNullOrEmpty(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;
                    string contentEscaped = item.Content.Replace("'", "''");
                    query = $@"INSERT INTO pu_supplier_detail_notes
                (Id, RequestCode, SeqNo, Content,
                 CreateAtTime, UserCreated, UpdateAtTime, UserUpdated)
               VALUES (
                '{rowId2}', '{s.RequestCode}', {seqNote},
                '{contentEscaped}',
                NOW(), '{currentUser}', NOW(), '{currentUser}'
               )";
                    await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    seqNote++;
                }
                query = $@"DELETE FROM pu_supplier_detail_doc_distribution WHERE RequestCode = '{s.RequestCode}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                int seqDist = 1;
                foreach (var item in documentDistributionHistory)
                {
                    string rowId3 = string.IsNullOrEmpty(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;
                    query = $@"
            INSERT INTO pu_supplier_detail_doc_distribution
            (
                Id,
                RequestCode,
                SeqNo,
                DocumentCode,
                DocumentName,
                Version,
                DistributionDate,
                DistributionTarget,
                IsChecked,
                CreateAtTime,
                UserCreated,
                UpdateAtTime,
                UserUpdated
            )
            VALUES
            (
                '{rowId3}',
                '{s.RequestCode}',
                {seqDist},
                '{item.DocumentCode}',
                '{item.DocumentName}',
                '{item.Version}',
                {(string.IsNullOrEmpty(item.DistributionDate) ? "NULL" : $"'{item.DistributionDate}'")},
                '{item.DistributionTarget}',
                {(item.IsChecked ? 1 : 0)},
                NOW(),
                '{currentUser}',
                NOW(),
                '{currentUser}'
            )
            ON DUPLICATE KEY UPDATE
                RequestCode = VALUES(RequestCode),
                SeqNo = VALUES(SeqNo),
                DocumentCode = VALUES(DocumentCode),
                DocumentName = VALUES(DocumentName),
                Version = VALUES(Version),
                DistributionDate = VALUES(DistributionDate),
                DistributionTarget = VALUES(DistributionTarget),
                IsChecked = VALUES(IsChecked),
                UpdateAtTime = NOW(),
                UserUpdated = '{currentUser}';
            ";
                    await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    seqDist++;
                }
                if (!string.IsNullOrEmpty(filesToDeleteJson))
                {
                    try
                    {
                        var filesToDeleteToken = JToken.Parse(filesToDeleteJson);
                        var idsToDelete = new List<string>();
                        if (filesToDeleteToken is JObject fObj)
                        {
                            foreach (var prop in fObj.Properties())
                            {
                                if (prop.Value is JArray propArray)
                                {
                                    idsToDelete.AddRange(
                                        propArray.Select(v => v?.ToString()).Where(v => !string.IsNullOrEmpty(v)));
                                }
                                else if (prop.Value != null && prop.Value.Type != JTokenType.Null)
                                {
                                    idsToDelete.Add(prop.Value.ToString());
                                }
                            }
                        }
                        else if (filesToDeleteToken is JArray fArr)
                        {
                            idsToDelete.AddRange(fArr.Select(v => v?.ToString()).Where(v => !string.IsNullOrEmpty(v)));
                        }
                        foreach (var _id in idsToDelete.Distinct())
                        {
                            query = $@"UPDATE pu_supplier_detail_attachment_file SET
                    IsDeleted = 1, UserUpdated = '{currentUser}', UpdateAtTime = NOW()
                    WHERE Id = '{_id}' AND RequestCode = '{s.RequestCode}'";
                            await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                        }
                    }
                    catch
                    {
                    }
                }
                string uploadRoot = Path.Combine(
                     Directory.GetCurrentDirectory(),
                     "wwwroot",
                     "Uploads",
                     "Purchasing",
                     "PUSupplierDetail",
                     s.RequestCode
                 );
                Directory.CreateDirectory(uploadRoot);
                async Task SaveAttachmentGroupAsync(string formFieldName, string fileGroup, string displayLabel)
                {
                    var files = form.Files.GetFiles(formFieldName);
                    foreach (var file in files)
                    {
                        if (file.Length <= 0) continue;

                        string documentTypeKey = null;
                        string originalFileName = file.FileName;
                        if (fileGroup == "DocumentCatalog" && originalFileName.Contains("__"))
                        {
                            var parts = originalFileName.Split(new[] { "__" }, 2, StringSplitOptions.None);
                            documentTypeKey = parts[0];
                            originalFileName = parts[1];
                        }

                        string savedFileName = $"{Guid.NewGuid():N}_{originalFileName}";
                        string fullPath = Path.Combine(uploadRoot, savedFileName);
                        using (var stream = new FileStream(fullPath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                        string relativePath = Path.Combine("Uploads", "Purchasing", "PUSupplierDetail", s.RequestCode, savedFileName).Replace("\\", "/");

                        string fileNameEscaped = originalFileName.Replace("'", "''");
                        query = $@"INSERT INTO pu_supplier_detail_attachment_file
                (Id, RequestCode, FileGroup, DocumentTypeKey, FileName, FilePath, FileSize, IsDeleted,
                 CreateAtTime, UserCreated, UpdateAtTime, UserUpdated)
               VALUES (
                '{Guid.NewGuid():N}', '{s.RequestCode}', '{fileGroup}',
                {(string.IsNullOrEmpty(documentTypeKey) ? "NULL" : $"'{documentTypeKey}'")},
                '{fileNameEscaped}', '{relativePath}', {file.Length}, 0,
                NOW(), '{currentUser}', NOW(), '{currentUser}'
               )
               ON DUPLICATE KEY UPDATE
                FileGroup = VALUES(FileGroup),
                DocumentTypeKey = VALUES(DocumentTypeKey),
                FileName = VALUES(FileName),
                FilePath = VALUES(FilePath),
                FileSize = VALUES(FileSize),
                IsDeleted = VALUES(IsDeleted),
                UpdateAtTime = NOW(),
                UserUpdated = '{currentUser}'";

                        await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);

                        string label = (fileGroup == "DocumentCatalog" && !string.IsNullOrEmpty(documentTypeKey))
                            ? $"{displayLabel} - {documentTypeKey}"
                            : displayLabel;

                        allTrackedFields.Add(new PUTrackedField
                        {
                            Label = label,
                            OldValue = "",
                            NewValue = originalFileName,
                            ModuleGroup = "Attachment"
                        });
                    }
                }
                await SaveAttachmentGroupAsync("DocumentCatalogFiles", "DocumentCatalog", "Tài liệu đính kèm");
                await SaveAttachmentGroupAsync("PaymentAccountRegistrationFormFiles", "PaymentAccountRegistrationForm", "Đơn đăng ký tài khoản thanh toán");
                await SaveAttachmentGroupAsync("BasicCommercialContractFiles", "BasicCommercialContract", "Hợp đồng thương mại cơ bản");
                await SaveAttachmentGroupAsync("NonDisclosureAgreementFiles", "NonDisclosureAgreement", "Thoả thuận bảo mật");
                await SaveAttachmentGroupAsync("TradingTermsAgreementFiles", "TradingTermsAgreement", "Điều khoản giao dịch");
                await SaveAttachmentGroupAsync("ConflictOfInterestDeclarationFiles", "ConflictOfInterestDeclaration", "Cam kết xung đột lợi ích");
                await SaveAttachmentGroupAsync("PriceComparisonFiles", "PriceComparison", "Bảng so sánh giá");
                await SaveAttachmentGroupAsync("BusinessRegistrationCertificateFiles", "BusinessRegistrationCertificate", "Giấy chứng nhận đăng ký kinh doanh");
                await SaveAttachmentGroupAsync("InvestmentLicenseFiles", "InvestmentLicense", "Giấy phép đầu tư");
                await SaveAttachmentGroupAsync("ProfilesCompanyFiles", "ProfilesCompany", "Hồ sơ năng lực công ty");
                allTrackedFields.AddRange(BuildTrackedFields(oldInfoRow, new Dictionary<string, (string, string, string)>
                {
                    ["CompanyName"] = ("CompanyName", s.CompanyName, "Directory"),
                    ["AdditionalCompanyName"] = ("AdditionalCompanyName", s.AdditionalCompanyName, "Directory"),
                    ["CompanyNameEN"] = ("CompanyNameEN", s.CompanyNameEN, "Directory"),
                    ["AdditionalCompanyNameEN"] = ("AdditionalCompanyNameEN", s.AdditionalCompanyNameEN, "Directory"),
                    ["TaxCode"] = ("TaxCode", s.TaxCode, "Directory"),
                    ["EstablishmentDate"] = ("EstablishmentDate", s.EstablishmentDate, "Directory"),
                    ["AuthorizedCapital"] = ("AuthorizedCapital", s.AuthorizedCapital, "Directory"),
                    ["Website"] = ("HeadOfficeWebsite", s.Website, "Directory"),
                    ["LegalRepresentativeName"] = ("Representative", s.LegalRepresentativeName, "Directory"),
                    ["PositionRole"] = ("RepresentativePosition", s.PositionRole, "Directory"),
                    ["YearOfBirth"] = ("RepresentativeYearOfBirth", s.YearOfBirth, "Directory"),
                    ["OfficialEmp"] = ("NumberEmployees", s.OfficialEmp, "Directory"),
                    ["TemporaryEmp"] = ("NumberEmployeeTemporary", s.TemporaryEmp, "Directory"),
                    ["BusinessType"] = ("BusinessType", s.BusinessType, "Directory"),
                    ["TypeCompany"] = ("TypeCompany", s.TypeCompany, "Directory"),
                    ["CountryOwnership"] = ("CountryOwnership", s.CountryOwnership, "Directory"),
                    ["ManufacturingEnterprise"] = ("ManufacturingEnterprise", s.ManufacturingEnterprise, "Directory"),
                    ["LegalForm"] = ("LegalForm", s.LegalForm, "Directory"),
                    ["SettlementMonth"] = ("SettlementMonth", s.SettlementMonth, "Directory"),
                    ["Listing"] = ("Listing", s.Listing, "Directory"),
                    ["CertificateISO9001"] = ("CertificateISO9001", s.CertificateISO9001, "Directory"),
                    ["CertificateISO14001"] = ("CertificateISO14001", s.CertificateISO14001, "Directory"),
                    ["CertificateOthers"] = ("CertificateOthers", s.CertificateOthers, "Directory"),
                    ["HeadOfficeAddressDetail"] = ("HeadOfficeAddressDetail", s.HeadOfficeAddressDetail, "Directory"),
                    ["HeadOfficeHouseNumber"] = ("HeadOfficeHouseNumber", s.HeadOfficeHouseNumber, "Directory"),
                    ["HeadOfficeStreet"] = ("HeadOfficeStreet", s.HeadOfficeStreet, "Directory"),
                    ["HeadOfficeDistrict"] = ("HeadOfficeDistrict", s.HeadOfficeDistrict, "Directory"),
                    ["HeadOfficeProvinceCity"] = ("HeadOfficeProvinceCity", s.HeadOfficeProvinceCity, "Directory"),
                    ["HeadOfficeCountry"] = ("HeadOfficeCountry", s.HeadOfficeCountry, "Directory"),
                    ["HeadOfficePhoneNumber"] = ("HeadOfficePhoneNumber", s.HeadOfficePhoneNumber, "Directory"),
                    ["TransactionOfficeAddressDetail"] = ("TransactionOfficeAddressDetail", s.TransactionOfficeAddressDetail, "Directory"),
                    ["TransactionOfficeHouseNumber"] = ("TransactionOfficeHouseNumber", s.TransactionOfficeHouseNumber, "Directory"),
                    ["TransactionOfficeStreet"] = ("TransactionOfficeStreet", s.TransactionOfficeStreet, "Directory"),
                    ["TransactionOfficeDistrict"] = ("TransactionOfficeDistrict", s.TransactionOfficeDistrict, "Directory"),
                    ["TransactionOfficeProvinceCity"] = ("TransactionOfficeProvinceCity", s.TransactionOfficeProvinceCity, "Directory"),
                    ["TransactionOfficeCountry"] = ("TransactionOfficeCountry", s.TransactionOfficeCountry, "Directory"),
                    ["TransactionOfficePhoneNumber"] = ("TransactionOfficePhoneNumber", s.TransactionOfficePhoneNumber, "Directory"),
                    ["TransactionOfficeGoogleMap"] = ("TransactionOfficeGoogleMap", s.TransactionOfficeGoogleMap, "Directory"),
                    ["CompanyNameMainCustomer1"] = ("CompanyNameMainCustomer1", s.CompanyNameMainCustomer1, "Directory"),
                    ["DependencyMainCustomer1"] = ("DependencyMainCustomer1", s.DependencyMainCustomer1, "Directory"),
                    ["NumberOfTradingYearsMainCustomer1"] = ("NumberOfTradingYearsMainCustomer1", s.NumberOfTradingYearsMainCustomer1, "Directory"),
                    ["CompanyNameMainCustomer2"] = ("CompanyNameMainCustomer2", s.CompanyNameMainCustomer2, "Directory"),
                    ["DependencyMainCustomer2"] = ("DependencyMainCustomer2", s.DependencyMainCustomer2, "Directory"),
                    ["NumberOfTradingYearsMainCustomer2"] = ("NumberOfTradingYearsMainCustomer2", s.NumberOfTradingYearsMainCustomer2, "Directory"),
                    ["CompanyNameMainCustomer3"] = ("CompanyNameMainCustomer3", s.CompanyNameMainCustomer3, "Directory"),
                    ["DependencyMainCustomer3"] = ("DependencyMainCustomer3", s.DependencyMainCustomer3, "Directory"),
                    ["NumberOfTradingYearsMainCustomer3"] = ("NumberOfTradingYearsMainCustomer3", s.NumberOfTradingYearsMainCustomer3, "Directory"),
                    ["CompanyNameSupplier1"] = ("CompanyNameSupplier1", s.CompanyNameSupplier1, "Directory"),
                    ["DependencySupplier1"] = ("DependencySupplier1", s.DependencySupplier1, "Directory"),
                    ["CompanyNameSupplier2"] = ("CompanyNameSupplier2", s.CompanyNameSupplier2, "Directory"),
                    ["DependencySupplier2"] = ("DependencySupplier2", s.DependencySupplier2, "Directory"),
                    ["NumberSubcontractor"] = ("NumberSubcontractor", s.NumberSubcontractor, "Directory"),
                    ["DependencySubcontractor"] = ("DependencySubcontractor", s.DependencySubcontractor, "Directory"),
                    ["DetailedIndustryClassification"] = ("DetailedIndustryClassification", s.DetailedIndustryClassification, "PU"),
                    ["TypeOfPurchasedGoods"] = ("TypeOfPurchasedGoods", s.TypeOfPurchasedGoods, "PU"),
                    ["PurchaseClassification"] = ("PurchaseClassification", s.PurchaseClassification, "PU"),
                    ["RepresentativeItemName"] = ("RepresentativeItemName", s.RepresentativeItemName, "PU"),
                    ["RepresentativeProductCode"] = ("RepresentativeProductCode", s.RepresentativeProductCode, "PU"),
                    ["MainMaker"] = ("MainMaker", s.MainMaker, "PU"),
                    ["PUManageResponsiblePerson"] = ("PUManageResponsiblePerson", s.PUResponsiblePerson, "PU"),
                    ["StatusSubmit"] = ("StatusSubmit", s.Status, "Directory"),
                    ["TradeName"] = ("TradeName", s.TradeName, "NewSupplierRequest"),
                    ["PUManageSupplierClassification"] = ("PUManageSupplierClassification", s.PUManageSupplierClassification, "NewSupplierRequest"),
                    ["Section"] = ("Section", s.Section, "NewSupplierRequest"),
                    ["Requester"] = ("Requester", s.Requester, "NewSupplierRequest"),
                    ["ReleasePOViaSAP"] = ("ReleasePOViaSAP", s.ReleasePOViaSAP, "PU"),
                    ["SelectionRequestReason"] = ("PurposeOfUse", s.SelectionRequestReason, "NewSupplierRequest"),
                    ["SpecialSelectionTarget"] = ("SpecialSelectionTarget", s.SpecialSelectionTarget, "NewSupplierRequest"),
                    ["InitialRegistrationDate"] = ("InitialRegistrationDate", s.InitialRegistrationDate, "NewSupplierRequest"),
                    ["LastTransaction"] = ("LastTransaction", s.LastTransaction, "NewSupplierRequest"),
                    ["Industry"] = ("Industry", s.Industry, "PU"),
                    ["VND"] = ("VND", s.VND?.ToString(), "PU"),
                    ["USD"] = ("USD", s.USD?.ToString(), "PU"),
                    ["Incoterm"] = ("Incoterm", s.Incoterm, "PU"),
                    ["InvestigationMethod"] = ("InvestigationMethod", s.InvestigationMethod, "QA"),
                    ["PurchasingDepartment"] = ("SetUp", s.PurchasingDepartment, "PU"),
                    ["QAPaymentTerms"] = ("QAPaymentTerms", s.QAPaymentTerms, "QA"),
                    ["PurchasingManagementDepartment"] = ("Creator", s.PurchasingManagementDepartment, "PU"),
                    ["PUManageUsingDepartment1"] = ("PUManageUsingDepartment1", s.PUManageUsingDepartment1, "PU"),
                    ["PUManageUsingDepartment2"] = ("PUManageUsingDepartment2", s.PUManageUsingDepartment2, "PU"),
                    ["BorrowedAssetClassification"] = ("BorrowedAssetClassification", s.BorrowedAssetClassification, "PU"),
                }));
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                double authorizedCapitalNum = 0;

                if (!string.IsNullOrWhiteSpace(s.AuthorizedCapital))
                {
                    double.TryParse(s.AuthorizedCapital, out authorizedCapitalNum);
                }
                query = $@"UPDATE pu_supplier_selection_requests
        SET HeadOfficeAddressDetail = '{EscapeSqlForMySql.Escape(s.HeadOfficeAddressDetail)}',
        MainMaker = '{EscapeSqlForMySql.Escape(s.MainMaker)}',
        RepresentativeItemName = '{EscapeSqlForMySql.Escape(s.RepresentativeItemName)}',
        PUManageResponsiblePerson = '{EscapeSqlForMySql.Escape(s.PUResponsiblePerson)}',
        HeadOfficePhoneNumber = '{EscapeSqlForMySql.Escape(s.HeadOfficePhoneNumber)}',
        HeadOfficeProvinceCity = '{EscapeSqlForMySql.Escape(s.HeadOfficeProvinceCity)}',
        HeadOfficeCountry = '{EscapeSqlForMySql.Escape(s.HeadOfficeCountry)}',
        HeadOfficeStreet = '{EscapeSqlForMySql.Escape(s.HeadOfficeStreet)}',
        HeadOfficeDistrict = '{EscapeSqlForMySql.Escape(s.HeadOfficeDistrict)}',
        HeadOfficeHouseNumber = '{EscapeSqlForMySql.Escape(s.HeadOfficeHouseNumber)}',
        TransactionOfficeHouseNumber = '{EscapeSqlForMySql.Escape(s.TransactionOfficeHouseNumber)}',
        TransactionOfficeStreet = '{EscapeSqlForMySql.Escape(s.TransactionOfficeStreet)}',
        TransactionOfficeDistrict = '{EscapeSqlForMySql.Escape(s.TransactionOfficeDistrict)}',
        TransactionOfficeProvinceCity = '{EscapeSqlForMySql.Escape(s.TransactionOfficeProvinceCity)}',
        TransactionOfficeCountry = '{EscapeSqlForMySql.Escape(s.TransactionOfficeCountry)}',
        TransactionOfficeAddressDetail = '{EscapeSqlForMySql.Escape(s.TransactionOfficeAddressDetail)}',
        TransactionOfficePhoneNumber = '{EscapeSqlForMySql.Escape(s.TransactionOfficePhoneNumber)}',
        CompanyName = '{EscapeSqlForMySql.Escape(s.CompanyName)}',
        BusinessType = '{EscapeSqlForMySql.Escape(s.BusinessType)}',
        AdditionalCompanyName = '{EscapeSqlForMySql.Escape(s.AdditionalCompanyName)}',
        CompanyNameEN = '{EscapeSqlForMySql.Escape(s.CompanyNameEN)}',
        AdditionalCompanyNameEN = '{EscapeSqlForMySql.Escape(s.AdditionalCompanyNameEN)}',
         TransactionOfficeAddressDetail = '{EscapeSqlForMySql.Escape(s.TransactionOfficeAddressDetail)}',
         EstablishmentDate = '{EscapeSqlForMySql.Escape(s.EstablishmentDate)}',
         AuthorizedCapital = {authorizedCapitalNum},
            Representative = '{EscapeSqlForMySql.Escape(s.LegalRepresentativeName)}',
            NumberEmployees = {ParseIntOrNull(s.OfficialEmp)},
            SettlementMonth = '{EscapeSqlForMySql.Escape(s.SettlementMonth)}',
            Listing = '{EscapeSqlForMySql.Escape(s.Listing)}',
             TypeOfPurchasedGoods = '{EscapeSqlForMySql.Escape(s.TypeOfPurchasedGoods)}',
             PurchaseClassification = '{EscapeSqlForMySql.Escape(s.PurchaseClassification)}',
         AuthorizedCapitalCurrency = '{EscapeSqlForMySql.Escape(s.AuthorizedCapitalCurrency)}'
        WHERE RequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);

                query = $@"UPDATE pu_supplier_selection_requests_directory
        SET HeadOfficeAddressDetail = '{EscapeSqlForMySql.Escape(s.HeadOfficeAddressDetail)}',
        MainMaker = '{EscapeSqlForMySql.Escape(s.MainMaker)}',
        MFilesResponseAt = '{EscapeSqlForMySql.Escape(s.InitialRegistrationDate)}',
        RepresentativeItemName = '{EscapeSqlForMySql.Escape(s.RepresentativeItemName)}',
        HeadOfficePhoneNumber = '{EscapeSqlForMySql.Escape(s.HeadOfficePhoneNumber)}',
        HeadOfficeProvinceCity = '{EscapeSqlForMySql.Escape(s.HeadOfficeProvinceCity)}',
        PUManageResponsiblePerson = '{EscapeSqlForMySql.Escape(s.PUResponsiblePerson)}',
        ReleasePOViaSAP = '{EscapeSqlForMySql.Escape(s.ReleasePOViaSAP)}',
        HeadOfficeCountry = '{EscapeSqlForMySql.Escape(s.HeadOfficeCountry)}',
        HeadOfficeStreet = '{EscapeSqlForMySql.Escape(s.HeadOfficeStreet)}',
        HeadOfficeDistrict = '{EscapeSqlForMySql.Escape(s.HeadOfficeDistrict)}',
        HeadOfficeHouseNumber = '{EscapeSqlForMySql.Escape(s.HeadOfficeHouseNumber)}',
        TransactionOfficeHouseNumber = '{EscapeSqlForMySql.Escape(s.TransactionOfficeHouseNumber)}',
        TransactionOfficeStreet = '{EscapeSqlForMySql.Escape(s.TransactionOfficeStreet)}',
        TransactionOfficeDistrict = '{EscapeSqlForMySql.Escape(s.TransactionOfficeDistrict)}',
        TransactionOfficeProvinceCity = '{EscapeSqlForMySql.Escape(s.TransactionOfficeProvinceCity)}',
        TransactionOfficeCountry = '{EscapeSqlForMySql.Escape(s.TransactionOfficeCountry)}',
        TransactionOfficeAddressDetail = '{EscapeSqlForMySql.Escape(s.TransactionOfficeAddressDetail)}',
        TransactionOfficePhoneNumber = '{EscapeSqlForMySql.Escape(s.TransactionOfficePhoneNumber)}',
        CompanyName = '{EscapeSqlForMySql.Escape(s.CompanyName)}',
        BusinessType = '{EscapeSqlForMySql.Escape(s.BusinessType)}',
        AdditionalCompanyName = '{EscapeSqlForMySql.Escape(s.AdditionalCompanyName)}',
        CompanyNameEN = '{EscapeSqlForMySql.Escape(s.CompanyNameEN)}',
        UsingDepartment1 = '{EscapeSqlForMySql.Escape(s.PUManageUsingDepartment1)}',
        UsingDepartment2 = '{EscapeSqlForMySql.Escape(s.PUManageUsingDepartment2)}',
        Section = '{EscapeSqlForMySql.Escape(s.Section)}',
        Requester = '{EscapeSqlForMySql.Escape(s.Requester)}',
        AdditionalCompanyNameEN = '{EscapeSqlForMySql.Escape(s.AdditionalCompanyNameEN)}',
         TransactionOfficeAddressDetail = '{EscapeSqlForMySql.Escape(s.TransactionOfficeAddressDetail)}',
         EstablishmentDate = '{EscapeSqlForMySql.Escape(s.EstablishmentDate)}',
         AuthorizedCapital = {authorizedCapitalNum},
            Representative = '{EscapeSqlForMySql.Escape(s.LegalRepresentativeName)}',
             NumberEmployees = {ParseIntOrNull(s.OfficialEmp)},
            SettlementMonth = '{EscapeSqlForMySql.Escape(s.SettlementMonth)}',
            Listing = '{EscapeSqlForMySql.Escape(s.Listing)}',
             TypeOfPurchasedGoods = '{EscapeSqlForMySql.Escape(s.TypeOfPurchasedGoods)}',
             PurchaseClassification = '{EscapeSqlForMySql.Escape(s.PurchaseClassification)}',
         AuthorizedCapitalCurrency = '{EscapeSqlForMySql.Escape(s.AuthorizedCapitalCurrency)}'
        WHERE RequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);


                query = $@"UPDATE pu_supplier_profiles_directory
        SET HeadOfficeAddressDetail = '{EscapeSqlForMySql.Escape(s.HeadOfficeAddressDetail)}',
        DetailedIndustryClassification = '{EscapeSqlForMySql.Escape(s.DetailedIndustryClassification)}',
        MainMaker = '{EscapeSqlForMySql.Escape(s.MainMaker)}',
        RepresentativeItemName = '{EscapeSqlForMySql.Escape(s.RepresentativeItemName)}',
        PUManageResponsiblePerson = '{EscapeSqlForMySql.Escape(s.PUResponsiblePerson)}',
        HeadOfficePhoneNumber = '{EscapeSqlForMySql.Escape(s.HeadOfficePhoneNumber)}',
        HeadOfficeProvinceCity = '{EscapeSqlForMySql.Escape(s.HeadOfficeProvinceCity)}',
        HeadOfficeCountry = '{EscapeSqlForMySql.Escape(s.HeadOfficeCountry)}',
        PurchaseClassification = '{EscapeSqlForMySql.Escape(s.PurchaseClassification)}',
        HeadOfficeStreet = '{EscapeSqlForMySql.Escape(s.HeadOfficeStreet)}',
        HeadOfficeDistrict = '{EscapeSqlForMySql.Escape(s.HeadOfficeDistrict)}',
        HeadOfficeHouseNumber = '{EscapeSqlForMySql.Escape(s.HeadOfficeHouseNumber)}',
        TransactionOfficeHouseNumber = '{EscapeSqlForMySql.Escape(s.TransactionOfficeHouseNumber)}',
        TransactionOfficeStreet = '{EscapeSqlForMySql.Escape(s.TransactionOfficeStreet)}',
        TransactionOfficeDistrict = '{EscapeSqlForMySql.Escape(s.TransactionOfficeDistrict)}',
        TransactionOfficeProvinceCity = '{EscapeSqlForMySql.Escape(s.TransactionOfficeProvinceCity)}',
        TransactionOfficeCountry = '{EscapeSqlForMySql.Escape(s.TransactionOfficeCountry)}',
        TransactionOfficeAddressDetail = '{EscapeSqlForMySql.Escape(s.TransactionOfficeAddressDetail)}',
        TransactionOfficeGoogleMap = '{EscapeSqlForMySql.Escape(s.TransactionOfficeGoogleMap)}',
        TransactionOfficePhoneNumber = '{EscapeSqlForMySql.Escape(s.TransactionOfficePhoneNumber)}',
        TypeCompany = '{EscapeSqlForMySql.Escape(s.TypeCompany)}',
        CompanyName = '{EscapeSqlForMySql.Escape(s.CompanyName)}',
        AdditionalCompanyName = '{EscapeSqlForMySql.Escape(s.AdditionalCompanyName)}',
        CompanyNameEN = '{EscapeSqlForMySql.Escape(s.CompanyNameEN)}',
        AdditionalCompanyNameEN = '{EscapeSqlForMySql.Escape(s.AdditionalCompanyNameEN)}',
         TransactionOfficeAddressDetail = '{EscapeSqlForMySql.Escape(s.TransactionOfficeAddressDetail)}',
         EstablishmentDate = '{EscapeSqlForMySql.Escape(s.EstablishmentDate)}',  
         HeadOfficeWebsite = '{EscapeSqlForMySql.Escape(s.Website)}',
         CertificateISO9001 = '{EscapeSqlForMySql.Escape((s.CertificateISO9001 == "true" || s.CertificateISO9001 == "1") ? "true" : "false")}',
         CertificateISO14001 = '{EscapeSqlForMySql.Escape((s.CertificateISO14001 == "true" || s.CertificateISO14001 == "1") ? "true" : "false")}',
         CertificateOthers = '{EscapeSqlForMySql.Escape(s.CertificateOthers)}',
         TaxCode = '{EscapeSqlForMySql.Escape(s.TaxCode)}',
         BusinessType = '{EscapeSqlForMySql.Escape(s.BusinessType)}',
         AuthorizedCapital = {authorizedCapitalNum},
            Representative = '{EscapeSqlForMySql.Escape(s.LegalRepresentativeName)}',
            RepresentativePosition = '{EscapeSqlForMySql.Escape(s.PositionRole)}',
            RepresentativeYearOfBirth = '{EscapeSqlForMySql.Escape(s.YearOfBirth)}',
          NumberEmployees = {ParseIntOrNull(s.OfficialEmp)},
            NumberEmployeeTemporary = {ParseIntOrNull(s.TemporaryEmp)},
            CountryOwnership = '{EscapeSqlForMySql.Escape(s.CountryOwnership)}',
            ManufacturingEnterprise = '{EscapeSqlForMySql.Escape(s.ManufacturingEnterprise)}',
            SettlementMonth = '{EscapeSqlForMySql.Escape(s.SettlementMonth)}',
            Listing = '{EscapeSqlForMySql.Escape(s.Listing)}',
            LegalForm = '{EscapeSqlForMySql.Escape(s.LegalForm)}',
             TypeOfPurchasedGoods = '{EscapeSqlForMySql.Escape(s.TypeOfPurchasedGoods)}',
         AuthorizedCapitalCurrency = '{EscapeSqlForMySql.Escape(s.AuthorizedCapitalCurrency)}',
         CompanyNameSupplier1 = '{EscapeSqlForMySql.Escape(s.CompanyNameMainCustomer1)}',
         DependencySupplier1 = {s.DependencyMainCustomer1 ?? "0"},
         CompanyNameSupplier2 = '{EscapeSqlForMySql.Escape(s.CompanyNameMainCustomer2)}',
         DependencySupplier2 = {s.DependencyMainCustomer2 ?? "0"},
        NumberSubcontractor = '{EscapeSqlForMySql.Escape(s.NumberSubcontractor ?? "0")}',
         DependencySubcontractor = '{EscapeSqlForMySql.Escape(s.DependencySubcontractor ?? "0")}'
        WHERE RequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);

                var quotationContacts = responsibleContact
                  .Where(x => x.Type == "QuotationOrder")
                  .ToList();


                string emails = string.Join(", ", quotationContacts.Select(x => x.Email));

                var primaryContact = quotationContacts.FirstOrDefault();

                string contactName = primaryContact == null
                   ? ""
                   : $"{primaryContact.PersonTitle} {primaryContact.FullName}".Trim();
                string contactPhone = primaryContact?.Phone ?? "";
                query = $@"UPDATE pu_supplier_email
        SET 
            EmailTo = '{EscapeSqlForMySql.Escape(emails)}',
            RecipientName = '{EscapeSqlForMySql.Escape(contactName)}',
            PhoneNumber = '{EscapeSqlForMySql.Escape(contactPhone)}'
        WHERE PURequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);

                var deleteQuery = $@"
            DELETE FROM pu_supplier_profiles_group_contact_directory
            WHERE RequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'";

                await ConnectDB.ExecuteNonQueryAsyncTransaction(deleteQuery, conn, trans);

                if (responsibleContact != null && responsibleContact.Count > 0)
                {
                    foreach (var contact in responsibleContact)
                    {
                        var id = Guid.NewGuid().ToString("N").Substring(0, 9);

                        var insertQuery = $@"
                INSERT INTO pu_supplier_profiles_group_contact_directory
                (
                    Id,
                    RequestCode,
                    PersonTitle,
                    LastName,
                    FullName,
                    Email,
                    Phone,
                    Type
                )
                VALUES
                (
                    '{id}',
                    '{EscapeSqlForMySql.Escape(s.RequestCode)}',
                    '{EscapeSqlForMySql.Escape(contact?.PersonTitle ?? "")}',
                    '{EscapeSqlForMySql.Escape(contact?.LastName ?? "")}',
                    '{EscapeSqlForMySql.Escape(contact?.FullName ?? "")}',
                    '{EscapeSqlForMySql.Escape(contact?.Email ?? "")}',
                    '{EscapeSqlForMySql.Escape(contact?.Phone ?? "")}',
                    '{EscapeSqlForMySql.Escape(contact?.Type ?? "")}'
                )";

                        await ConnectDB.ExecuteNonQueryAsyncTransaction(insertQuery, conn, trans);
                    }
                }
                query = $@"DELETE FROM pu_supplier_profiles_account_banks_directory 
   WHERE RequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);

                int seqAccountDirectory = 1;
                foreach (var item in paymentAccount)
                {
                    string accId = Guid.NewGuid().ToString("N").Substring(0, 9);
                    query = $@"
           INSERT INTO pu_supplier_profiles_account_banks_directory
                (
                    Id,
                    RequestCode,
                    SeqNo,
                    AccountType,
                    AccountName,
                    AccountNumber,
                    BankName,
                    BranchAccountAddress,
                    BranchAccountName,
                    BranchAccountSWIFTCode,
                    CitadCode,
                    ReasonTransactionByUSD,
                    ObjectTransactionByUS,
                    IDAccountBank
                )
                VALUES
                (
                    '{accId}',
                    '{EscapeSqlForMySql.Escape(s.RequestCode)}',
                    {seqAccountDirectory},
                    '{EscapeSqlForMySql.Escape(item.AccountType)}',
                    '{EscapeSqlForMySql.Escape(item.AccountName)}',
                    '{EscapeSqlForMySql.Escape(item.AccountNumber)}',
                    '{EscapeSqlForMySql.Escape(item.BankName)}',
                    '{EscapeSqlForMySql.Escape(item.BranchAccountAddress)}',
                    '{EscapeSqlForMySql.Escape(item.BranchAccountName)}',
                    '{EscapeSqlForMySql.Escape(item.BranchAccountSWIFTCode)}',
                    '{EscapeSqlForMySql.Escape(item.CitadCode)}',
                    '{EscapeSqlForMySql.Escape(item.ReasonTransactionByUSD)}',
                    '{EscapeSqlForMySql.Escape(item.ObjectTransactionByUS)}',
                    '{EscapeSqlForMySql.Escape(item.IdAccountBank)}'
                )
                ON DUPLICATE KEY UPDATE
                    RequestCode = VALUES(RequestCode),
                    SeqNo = VALUES(SeqNo),
                    AccountType = VALUES(AccountType),
                    AccountName = VALUES(AccountName),
                    AccountNumber = VALUES(AccountNumber),
                    BankName = VALUES(BankName),
                    BranchAccountAddress = VALUES(BranchAccountAddress),
                    BranchAccountName = VALUES(BranchAccountName),
                    BranchAccountSWIFTCode = VALUES(BranchAccountSWIFTCode),
                    CitadCode = VALUES(CitadCode),
                    ReasonTransactionByUSD = VALUES(ReasonTransactionByUSD),
                    ObjectTransactionByUS = VALUES(ObjectTransactionByUS),
                    IDAccountBank = VALUES(IDAccountBank);";
                    await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    seqAccountDirectory++;
                }
                if (performanceRanking != null && performanceRanking.Count > 0)
                {
                    foreach (var item in performanceRanking)
                    {
                        query = $@"
                INSERT INTO pu_manage_control_confirm_selection_supplier_directory
                (
                    RequestCode,
                    LoanedAssetType,
                    SupplierRanking,
                    QAPaymentTerms,
                    LastTransactionDate,
                    OverallRating
                )
                VALUES
                (
                    '{EscapeSqlForMySql.Escape(s.RequestCode)}',
                    '{EscapeSqlForMySql.Escape(s.BorrowedAssetClassification)}',
                    '{EscapeSqlForMySql.Escape(item?.SupplierRanking ?? "")}',
                    '{EscapeSqlForMySql.Escape(s.QAPaymentTerms)}',
                    '{EscapeSqlForMySql.Escape(s.LastTransaction)}',
                    '{EscapeSqlForMySql.Escape(item?.ComprehensiveEvaluationRankingPoint ?? "")}'
                )
                ON DUPLICATE KEY UPDATE
                    LoanedAssetType = VALUES(LoanedAssetType),
                    SupplierRanking = VALUES(SupplierRanking),
                    QAPaymentTerms = VALUES(QAPaymentTerms),
                    LastTransactionDate = VALUES(LastTransactionDate),
                    OverallRating = VALUES(OverallRating);";

                        await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    }
                }

                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                string InvestigationDatePlanned = "";
                string InvestigationScorePlanned = "";
                string QAInvestigationDateOnsite = "";


                if (additionalInvestigationRows != null && additionalInvestigationRows.Count > 0)
                {
                    InvestigationDatePlanned = string.Join(",",
                        additionalInvestigationRows
                            .Where(x =>
                                x.Type == "purchasing" &&
                                x.onsite != null &&
                                !string.IsNullOrWhiteSpace(x.onsite.date))
                            .Select(x => x.onsite.date));

                    InvestigationScorePlanned = string.Join(",",
                        additionalInvestigationRows
                            .Where(x =>
                                x.Type == "purchasing" &&
                                x.onsite != null &&
                                !string.IsNullOrWhiteSpace(x.onsite.resultPoint))
                            .Select(x => x.onsite.resultPoint));

                    QAInvestigationDateOnsite = string.Join(",",
                        additionalInvestigationRows
                            .Where(x =>
                                x.Type == "quality" &&
                                x.onsite != null &&
                                !string.IsNullOrWhiteSpace(x.onsite.date))
                            .Select(x => x.onsite.date));
                }

                query = $@"UPDATE pu_pr_confirm_requirement_selection_supplier_directory
        SET VND = '{s.VND}',
         USD = '{s.USD}' ,
        TradeName = '{EscapeSqlForMySql.Escape(s.TradeName)}',
        Incoterm = '{EscapeSqlForMySql.Escape(s.Incoterm)}',
        InvestigationDatePlanned = '{EscapeSqlForMySql.Escape(InvestigationDatePlanned)}',
        InvestigationScorePlanned = '{EscapeSqlForMySql.Escape(InvestigationScorePlanned)}',
        Industry = '{EscapeSqlForMySql.Escape(s.Industry)}'
        WHERE RequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);

                query = $@"UPDATE pu_manage_control_confirm_selection_supplier_directory
        SET 
        QAInvestigationDateOnsite = '{EscapeSqlForMySql.Escape(QAInvestigationDateOnsite)}'
        WHERE RequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                if (actualContractExecution != null && actualContractExecution.Count > 0)
                {
                    var typeMappings = new Dictionary<string, (string RevisionColumn, string DateColumn, string AppendicesColumn)>
            {
                { "BasicCommercialContract", ("BTARevision", "BasicCommercialSigningDate", "BTAAppendix") },
                { "NonDisclosureAgreement", ("NDARevision", "NonDisclosureSigningDate", "NDAAppendix") },
                { "ConflictOfInterestDeclaration", ("COIRevision", "COISignedDate", null) },
                { "QualityAssuranceAgreement", (null, "QAReceivingDay", null) }
            };

                    var columnValues = new Dictionary<string, List<string>>();

                    foreach (var item in actualContractExecution)
                    {
                        if (!typeMappings.TryGetValue(item.Type, out var mapping))
                            continue;

                        if (!string.IsNullOrWhiteSpace(mapping.RevisionColumn)
                            && !string.IsNullOrWhiteSpace(item.Rev))
                        {
                            if (!columnValues.ContainsKey(mapping.RevisionColumn))
                                columnValues[mapping.RevisionColumn] = new List<string>();

                            columnValues[mapping.RevisionColumn]
                                .Add(EscapeSqlForMySql.Escape(item.Rev));
                        }

                        if (!string.IsNullOrWhiteSpace(item.DateExecutionEvaluateSupplier))
                        {
                            if (!columnValues.ContainsKey(mapping.DateColumn))
                                columnValues[mapping.DateColumn] = new List<string>();

                            columnValues[mapping.DateColumn]
                                .Add(EscapeSqlForMySql.Escape(item.DateExecutionEvaluateSupplier));
                        }

                        if (!string.IsNullOrWhiteSpace(mapping.AppendicesColumn)
                            && !string.IsNullOrWhiteSpace(item.Appendices))
                        {
                            if (!columnValues.ContainsKey(mapping.AppendicesColumn))
                                columnValues[mapping.AppendicesColumn] = new List<string>();

                            columnValues[mapping.AppendicesColumn]
                                .Add(EscapeSqlForMySql.Escape(item.Appendices));
                        }
                    }

                    string dynamicInsertCol = "";
                    string dynamicInsertVal = "";
                    string dynamicUpdateSet = "";

                    if (columnValues.Any())
                    {
                        dynamicInsertCol = ", " + string.Join(", ", columnValues.Keys);

                        dynamicInsertVal = ", " + string.Join(
                            ", ",
                            columnValues.Values.Select(v =>
                                $"'{string.Join(",", v)}'")
                        );

                        dynamicUpdateSet = ", " + string.Join(
                            ", ",
                            columnValues.Keys.Select(c =>
                                $"{c} = VALUES({c})")
                        );
                    }

                    query = $@"
            INSERT INTO pu_manage_control_confirm_selection_supplier_directory
            (
                RequestCode,
                LoanedAssetType,
                QAPaymentTerms,
                LastTransactionDate
                {dynamicInsertCol}
            )
            VALUES
            (
                '{EscapeSqlForMySql.Escape(s.RequestCode)}',
                '{EscapeSqlForMySql.Escape(s.BorrowedAssetClassification)}',
                '{EscapeSqlForMySql.Escape(s.QAPaymentTerms)}',
                '{EscapeSqlForMySql.Escape(s.LastTransaction)}'
                {dynamicInsertVal}
            )
            ON DUPLICATE KEY UPDATE
                LoanedAssetType = VALUES(LoanedAssetType),
                QAPaymentTerms = VALUES(QAPaymentTerms),
                LastTransactionDate = VALUES(LastTransactionDate)
                {dynamicUpdateSet};";

                    await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                }
                var deletionQuery = $@"
            DELETE FROM pu_supplier_profiles_revenues_directory
            WHERE RequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'
              AND Type = 'Office';";

                await ConnectDB.ExecuteNonQueryAsyncTransaction(deletionQuery, conn, trans);

                if (revenuePerformance != null && revenuePerformance.Count > 0)
                {
                    foreach (var item in revenuePerformance)
                    {
                        double? rpRevenue = double.TryParse(
                            item?.Revenue?.Replace(",", ""),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var rv)
                            ? rv
                            : (double?)null;

                        query = $@"
                    INSERT INTO pu_supplier_profiles_revenues_directory
                    (
                        Id,
                        RequestCode,
                        FiscalYear,
                        Revenue,
                        RevenueUnit,
                        Type
                    )
                    VALUES
                    (
                        UUID(),
                        '{EscapeSqlForMySql.Escape(s.RequestCode)}',
                        '{EscapeSqlForMySql.Escape(item?.Year ?? "")}',
                        '{rpRevenue?.ToString(CultureInfo.InvariantCulture) ?? "0"}',
                        '{EscapeSqlForMySql.Escape(item?.RevenueCurrency ?? "")}',
                        'Office'
                    );";

                        await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                    }
                }



                string puStatus = (s.Status == "Active" || s.Status == "Block" || s.Status == "Reviewing") ? "1" : "0";

                var supplierNameVN = s.CompanyName + " " + s.AdditionalCompanyName;
                query = $@"UPDATE pu_supplier_email
        SET StatusEmailEnabled = '{EscapeSqlForMySql.Escape(puStatus)}',
            IndustryCode = '{EscapeSqlForMySql.Escape(s.Industry)}',
            Maker = '{EscapeSqlForMySql.Escape(s.MainMaker)}',
            IndustryField = '{EscapeSqlForMySql.Escape(s.DetailedIndustryClassification)}',
            TradeName = '{EscapeSqlForMySql.Escape(s.TradeName)}',
            SupplierCode = '{EscapeSqlForMySql.Escape(s.SupplierCode)}',
            SupplierName = '{EscapeSqlForMySql.Escape(supplierNameVN)}'
        WHERE PURequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                query = $@"UPDATE qa_document_supplier_manage_list 
                        SET SupplierName = '{EscapeSqlForMySql.Escape(supplierNameVN.Trim())}',
                         SupplierCode = '{EscapeSqlForMySql.Escape(s.SupplierCode)}', PersonInCharge = '{EscapeSqlForMySql.Escape(s.Requester)}', 
                        SectionCode = '{EscapeSqlForMySql.Escape(s.Section)}'
                        WHERE PURequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}';";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                var isDirectoryDt = ConnectDB.ExecuteQuery(
                  $@"SELECT 1 FROM pu_supplier_selection_requests_directory 
             WHERE RequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}' LIMIT 1");
                bool isDirectory = isDirectoryDt.Rows.Count > 0;
                string suffix = isDirectory ? "_directory" : "";
                query = $@"UPDATE pu_supplier_selection_requests{suffix} SET
              
                SupplierStatus = '{EscapeSqlForMySql.Escape(s.Status)}',
                PUManageSupplierClassification = '{EscapeSqlForMySql.Escape(s.PUManageSupplierClassification)}',
                HeadOfficeHouseNumber = '{EscapeSqlForMySql.Escape(s.HeadOfficeHouseNumber)}',
                HeadOfficeStreet = '{EscapeSqlForMySql.Escape(s.HeadOfficeStreet)}',
                HeadOfficeDistrict = '{EscapeSqlForMySql.Escape(s.HeadOfficeDistrict)}',
                HeadOfficeProvinceCity = '{EscapeSqlForMySql.Escape(s.HeadOfficeProvinceCity)}',
                HeadOfficeCountry = '{EscapeSqlForMySql.Escape(s.HeadOfficeCountry)}'
                WHERE RequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                query = $@"UPDATE pu_manage_control_confirm_selection_supplier{suffix} SET
        SupplierCode = '{EscapeSqlForMySql.Escape(s.SupplierCode)}'
        WHERE RequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);
                query = $@"UPDATE pu_pr_confirm_requirement_selection_supplier{suffix} SET
        Industry = '{EscapeSqlForMySql.Escape(s.Industry)}'
        WHERE RequestCode = '{EscapeSqlForMySql.Escape(s.RequestCode)}'";
                await ConnectDB.ExecuteNonQueryAsyncTransaction(query, conn, trans);

                await SaveChangeHistoryAsync(s.RequestCode, currentUser, allTrackedFields, conn, trans);
                await trans.CommitAsync();
                msg.result = true;
                msg.message = lang.TranslateContent(Request.Headers["CurrentLanguage"].ToString(), "Supplier information has been updated successfully", "Cập nhật thông tin nhà cung cấp thành công", "");
                var dtLegalForm = ConnectDB.ExecuteQuery($@"SELECT Id FROM pu_supplier_legal_form WHERE LegalForm = '{s.LegalForm}'");
                string companyLegalFormCode = "";
                if (dtLegalForm.Rows.Count > 0 && dtLegalForm.Rows[0]["Id"] != DBNull.Value)
                {
                    companyLegalFormCode = dtLegalForm.Rows[0]["Id"].ToString().PadLeft(2, '0');
                }
                var syncedContactDt = ConnectDB.ExecuteQuery(
                    $@"SELECT Id FROM pu_supplier_detail_contact WHERE RequestCode = '{s.RequestCode}'");
                var syncedBankDt = ConnectDB.ExecuteQuery(
                    $@"SELECT Id FROM pu_supplier_detail_account_bank WHERE RequestCode = '{s.RequestCode}'");
                try
                {
                    List<string> supplierEmails = responsibleContact
                        .Where(c => !string.IsNullOrWhiteSpace(c.Email))
                        .Select(c => c.Email)
                        .Distinct()
                        .ToList();
                    int category = -1;
                    string partnerCode = s.SupplierCode.ToString();

                    if (partnerCode?.Contains("AS-F", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        category = 2;
                    }

                    else if (partnerCode?.Contains("AS-D", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        category = 1;
                    }
                    var mfilesResult = await MFilesHelper.RequestInsertSupplierFieldsAtMFiles(
                        _mfilesAPI,
                        s.RequestCode,
                        s.CompanyName,
                        category,
                        s.SupplierCode,
                        s.TaxCode,
                        supplierEmails
                    );
                    var mfilesData = (dynamic)mfilesResult.Value;
                    if (mfilesData != null && mfilesData.success != true)
                    {
                        msg.message += lang.TranslateContent(
                                         Request.Headers["CurrentLanguage"].ToString(),
                                         $" Database updated successfully but M-Files synchronization failed: {mfilesData.message}",
                                         $" Cập nhật DB thành công nhưng đồng bộ M-Files thất bại: {mfilesData.message}",
                                         ""
                                         );
                    }
                }
                catch (Exception mfilesEx)
                {
                    msg.message += $" (Cập nhật DB thành công nhưng đẩy M-Files lỗi: {mfilesEx.Message})";
                }
                if (!string.IsNullOrEmpty(sendInfoJson))
                {
                    JObject sendObj = JObject.Parse(sendInfoJson);
                    List<string> receiver = sendObj["Receiver"] is JArray receiverArray ? receiverArray.Select(r => r.ToString()).ToList() : new List<string>();
                    List<string> cc = sendObj["CC"] is JArray ccArray ? ccArray.Select(r => r.ToString()).ToList() : new List<string>();
                    string subject = sendObj["Subject"]?.ToString() ?? "";
                    string purchasingControl = sendObj["PurchasingControl"]?.ToString() ?? "";
                    Message mailMsg = SendMailSupplierUpdateCore(
                        receiver,
                        cc,
                        subject,
                        purchasingControl,
                        s.CompanyName + " " + s.AdditionalCompanyName,
                        s.SupplierCode,
                        DateTime.Now.ToString("yyyy-MM-dd"),
                        s.RequestCode,
                        "PUManageSupplierDetail");
                    if (mailMsg.result == false)
                    {
                        msg.message += lang.TranslateContent(
                             Request.Headers["CurrentLanguage"].ToString(),
                             $" (Database updated successfully but email sending failed: {mailMsg.message})",
                             $" (Cập nhật DB thành công nhưng gửi mail thất bại: {mailMsg.message})",
                             ""
                         );

                    }
                }
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                msg.result = false;
                msg.message = ex.Message;
            }
            return Ok(msg);
        }