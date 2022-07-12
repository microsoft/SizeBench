using System.ComponentModel.DataAnnotations;

namespace SizeBench.ExcelExporter.Tests;

[TestClass]
public class ClosedXMLExporterTests
{
    public class TestTypeWithDisplayAttributeUsage
    {
        // Fields and methods should not show up, so let's have some to prove that
#pragma warning disable CA1822 // Mark members as static - this is trying to test fields and methods explicitly.
#pragma warning disable CA1051 // Do not declare visible instance fields - this type is only used for tests
        public bool IsFooField;
#pragma warning restore CA1051 // Do not declare visible instance fields
        public bool Method() => false;
#pragma warning restore CA1822 // Mark members as static

        public bool IsFoo { get; }
        public int Bar { get; set; }

        [Display(Name = "Is A Foo")]
        public bool IsFooWithDisplayName { get; }
        [Display(Name = "Bar Amount")]
        public int BarWithDisplayName { get; set; }
        [Display(AutoGenerateField = false)]
        public bool FieldThatShouldNotAutoGenerate { get; }
    }

    public class TestDerivedTypeWithDisplayAttributeUsage : TestTypeWithDisplayAttributeUsage
    {
#pragma warning disable CA1822 // Mark members as static - this is trying to test fields and methods explicitly.
#pragma warning disable CA1051 // Do not declare visible instance fields - this type is only used for tests
        public bool IsZzzField;
#pragma warning restore CA1051 // Do not declare visible instance fields
        public int DerivedMethod() => 0;
#pragma warning restore CA1822

        public bool IsZzz { get; }

        [Display(Name = "Zzz?")]
        public bool ZzzWithdisplayAttribute { get; set; }
    }

    public class TestTypeWithFormatAttributeUsage
    {
        [Display(Name = "Relative Virtual Address")]
        [DisplayFormat(DataFormatString = "0x{0:X}")]
        public uint RVA { get; protected set; }

        [Display(Name = "Type Name")]
        [DisplayFormat(NullDisplayText = "??")]
        public string? Name { get; protected set; }
    }

    public class TestDerivedTypeWithFormatAttributeUsage : TestTypeWithFormatAttributeUsage
    {
        public TestDerivedTypeWithFormatAttributeUsage(uint rva, string? typeName, uint? length, bool isBlah)
        {
            this.RVA = rva;
            this.Name = typeName;
            this.Length = length;
            this.IsBlah = isBlah;
        }

        [DisplayFormat(DataFormatString = "{0:X}", NullDisplayText = "I Dunno")]
        public uint? Length { get; set; }

        [Display(Name = "Is Blah")]
        public bool IsBlah { get; }
    }

    public class TestTypeWithCollectionProperty
    {
        [Display(Name = "Test String")]
        [DisplayFormat(DataFormatString = "{0}", NullDisplayText = "Whoa...")]
        public string? TestString { get; }

        [Display(Name = "Enumerable Name")]
        [DisplayFormat(DataFormatString = "{0:junk}")]
        public IEnumerable<int>? TestEnumerable { get; }
    }

    public class TestTypeWithDisplayAttributeUsingOrder
    {
        [Display(Name = "Another Property", Order = 2)]
        public int Property1 { get; }

        public int Property2 { get; }

        [Display(Name = "The Last Property")]
        public int Property3 { get; }

        [Display(Name = "A Property", Order = 1)]
        public int Property4 { get; }
    }

    [TestMethod]
    public void DetermineColumnHeadersUsesDisplayAttributeIfPresent()
    {
        var columnMetadata = ClosedXMLExporter.DetermineColumnHeaders(typeof(TestTypeWithDisplayAttributeUsage));
        Assert.AreEqual(4, columnMetadata.Count);

        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Is A Foo"));
        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "IsFoo"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "IsFooWithDisplayName"));
        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Bar Amount"));
        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Bar"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "BarWithDisplayName"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "FieldThatShouldNotAutoGenerate"));
        Assert.IsNull(columnMetadata[0].displayFormatAttribute);
        Assert.IsNull(columnMetadata[1].displayFormatAttribute);
        Assert.IsNull(columnMetadata[2].displayFormatAttribute);
        Assert.IsNull(columnMetadata[3].displayFormatAttribute);
    }

    [TestMethod]
    public void DetermineColumnHeadersUsesDisplayAttributeFromBaseClass()
    {
        var columnMetadata = ClosedXMLExporter.DetermineColumnHeaders(typeof(TestDerivedTypeWithDisplayAttributeUsage));
        Assert.AreEqual(6, columnMetadata.Count);

        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Is A Foo"));
        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "IsFoo"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "IsFooWithDisplayName"));
        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Bar Amount"));
        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Bar"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "BarWithDisplayName"));
        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "IsZzz"));
        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Zzz?"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "ZzzWithDisplayAttribute"));
        Assert.IsNull(columnMetadata[0].displayFormatAttribute);
        Assert.IsNull(columnMetadata[1].displayFormatAttribute);
        Assert.IsNull(columnMetadata[2].displayFormatAttribute);
        Assert.IsNull(columnMetadata[3].displayFormatAttribute);
        Assert.IsNull(columnMetadata[4].displayFormatAttribute);
        Assert.IsNull(columnMetadata[5].displayFormatAttribute);
    }

    [TestMethod]
    public void DetermineColumnHeadersFindsDisplayFormatAttributes()
    {
        var columnMetadata = ClosedXMLExporter.DetermineColumnHeaders(typeof(TestTypeWithFormatAttributeUsage));
        Assert.AreEqual(2, columnMetadata.Count);

        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Relative Virtual Address"));
        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Type Name"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "Name"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "RVA"));

        int indexOfRVA = -1, indexOfTypeName = -1;
        for (var i = 0; i < 2; i++)
        {
            if (columnMetadata[i].columnHeader == "Relative Virtual Address")
            {
                indexOfRVA = i;
            }
            else if (columnMetadata[i].columnHeader == "Type Name")
            {
                indexOfTypeName = i;
            }
        }
        Assert.AreEqual("0x{0:X}", columnMetadata[indexOfRVA].displayFormatAttribute!.DataFormatString);
        Assert.AreEqual("??", columnMetadata[indexOfTypeName].displayFormatAttribute!.NullDisplayText);
        Assert.IsNull(columnMetadata[indexOfTypeName].displayFormatAttribute!.DataFormatString);
    }

    [TestMethod]
    public void DetermineColumnHeadersOrdersByDisplayAttributeOrderPropertyIfPresentThenByDeclarationOrder()
    {
        var columnMetadata = ClosedXMLExporter.DetermineColumnHeaders(typeof(TestTypeWithDisplayAttributeUsingOrder));
        Assert.AreEqual(4, columnMetadata.Count);

        Assert.AreEqual("A Property", columnMetadata[0].columnHeader);
        Assert.AreEqual("Another Property", columnMetadata[1].columnHeader);
        Assert.AreEqual("Property2", columnMetadata[2].columnHeader);
        Assert.AreEqual("The Last Property", columnMetadata[3].columnHeader);
    }

    [TestMethod]
    public void DetermineColumnHeadersFindsDisplayFormatAttributesFromBaseClass()
    {
        var columnMetadata = ClosedXMLExporter.DetermineColumnHeaders(typeof(TestDerivedTypeWithFormatAttributeUsage));
        Assert.AreEqual(4, columnMetadata.Count);

        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Relative Virtual Address"));
        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Type Name"));
        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Length"));
        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Is Blah"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "Name"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "RVA"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "IsBlah"));

        int indexOfRVA = -1, indexOfTypeName = -1, indexOfLength = -1, indexOfIsBlah = -1;
        for (var i = 0; i < 4; i++)
        {
            if (columnMetadata[i].columnHeader == "Relative Virtual Address")
            {
                indexOfRVA = i;
            }
            else if (columnMetadata[i].columnHeader == "Type Name")
            {
                indexOfTypeName = i;
            }
            else if (columnMetadata[i].columnHeader == "Length")
            {
                indexOfLength = i;
            }
            else if (columnMetadata[i].columnHeader == "Is Blah")
            {
                indexOfIsBlah = i;
            }
        }
        Assert.AreEqual("0x{0:X}", columnMetadata[indexOfRVA].displayFormatAttribute!.DataFormatString);
        Assert.AreEqual("??", columnMetadata[indexOfTypeName].displayFormatAttribute!.NullDisplayText);
        Assert.IsNull(columnMetadata[indexOfTypeName].displayFormatAttribute!.DataFormatString);
        Assert.AreEqual("I Dunno", columnMetadata[indexOfLength].displayFormatAttribute!.NullDisplayText);
        Assert.AreEqual("{0:X}", columnMetadata[indexOfLength].displayFormatAttribute!.DataFormatString);
        Assert.IsNull(columnMetadata[indexOfIsBlah].displayFormatAttribute);
    }

    [TestMethod]
    public void DetermineColumnHeadersSkipsCollectionsExceptStrings()
    {
        var columnMetadata = ClosedXMLExporter.DetermineColumnHeaders(typeof(TestTypeWithCollectionProperty));
        Assert.AreEqual(1, columnMetadata.Count);

        Assert.IsTrue(columnMetadata.Any(cm => cm.columnHeader == "Test String"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "TestString"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "Enumerable Name"));
        Assert.IsFalse(columnMetadata.Any(cm => cm.columnHeader == "TestEnumerable"));

        Assert.AreEqual("{0}", columnMetadata[0].displayFormatAttribute!.DataFormatString);
        Assert.AreEqual("Whoa...", columnMetadata[0].displayFormatAttribute!.NullDisplayText);
    }

    [TestMethod]
    public void GetTableRespectsDisplayFormatAttribute()
    {
        var items = new List<TestDerivedTypeWithFormatAttributeUsage>()
            {
                new TestDerivedTypeWithFormatAttributeUsage(0x500, "Type 1", 32, false),
                new TestDerivedTypeWithFormatAttributeUsage(0x100, null, null, true)
            };

        var columnMetadata = ClosedXMLExporter.DetermineColumnHeaders(typeof(TestDerivedTypeWithFormatAttributeUsage));
        var results = ClosedXMLExporter.GetTable(items, columnMetadata, null, CancellationToken.None);

        Assert.AreEqual(items.Count * columnMetadata.Count, results.Length);

        int indexOfRVA = -1, indexOfTypeName = -1, indexOfLength = -1, indexOfIsBlah = -1;
        for (var i = 0; i < 4; i++)
        {
            if (columnMetadata[i].columnHeader == "Relative Virtual Address")
            {
                indexOfRVA = i;
            }
            else if (columnMetadata[i].columnHeader == "Type Name")
            {
                indexOfTypeName = i;
            }
            else if (columnMetadata[i].columnHeader == "Length")
            {
                indexOfLength = i;
            }
            else if (columnMetadata[i].columnHeader == "Is Blah")
            {
                indexOfIsBlah = i;
            }
        }

        Assert.AreEqual("0x500", results[0, indexOfRVA]);
        Assert.AreEqual("0x100", results[1, indexOfRVA]);
        Assert.AreEqual("Type 1", results[0, indexOfTypeName]);
        Assert.AreEqual("??", results[1, indexOfTypeName]);
        Assert.AreEqual("20", results[0, indexOfLength]);
        Assert.AreEqual("I Dunno", results[1, indexOfLength]);
        Assert.AreEqual("False", results[0, indexOfIsBlah]);
        Assert.AreEqual("True", results[1, indexOfIsBlah]);
    }
}
