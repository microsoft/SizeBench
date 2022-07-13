using System.Windows;
using System.Windows.Controls;
using SizeBench.TestInfrastructure;
using SizeBench.GUI.Controls.Errors;

namespace SizeBench.GUI.Tests;

[STATestClass]
public sealed class AppWideDialogTemplateSelectorTests
{
    public FrameworkElement? TestElement { get; private set; }
    public DataTemplate? MessageTemplate { get; private set; }
    public DataTemplate? ErrorTemplate { get; private set; }

    [TestInitialize]
    public void TestInitialize()
    {
        this.MessageTemplate = new DataTemplate();
        this.ErrorTemplate = new DataTemplate();

        this.TestElement = new Button()
        {
            Resources = new ResourceDictionary()
        };

        this.TestElement.Resources.Add("MessageTemplate", this.MessageTemplate);
        this.TestElement.Resources.Add("ErrorTemplate", this.ErrorTemplate);
    }

    [TestMethod]
    public void BadInputsReturnsNull()
    {
        Assert.IsNull(AppWideDialogTemplateSelector.Instance.SelectTemplate(null, this.TestElement!));
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.  This test is explicitly trying to test null.
        Assert.IsNull(AppWideDialogTemplateSelector.Instance.SelectTemplate(new AppWideModalMessageDialogViewModel("title", "message"), null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        Assert.IsNull(AppWideDialogTemplateSelector.Instance.SelectTemplate(123, this.TestElement!));
    }

    [TestMethod]
    public void ProgressOnlyShowsMessageTemplate()
    {
        using var dialogVM = new AppWideModalProgressOnlyDialogViewModel("title", "message", isCancelable: true, (ct) => Task.CompletedTask);
        var result = AppWideDialogTemplateSelector.Instance.SelectTemplate(dialogVM, this.TestElement!);
        Assert.IsTrue(ReferenceEquals(result, this.MessageTemplate));
    }

    [TestMethod]
    public void MessageViewModelShowsMessageTemplate()
    {
        var dialogVM = new AppWideModalMessageDialogViewModel("title", "message");
        var result = AppWideDialogTemplateSelector.Instance.SelectTemplate(dialogVM, this.TestElement!);
        Assert.IsTrue(ReferenceEquals(result, this.MessageTemplate));
    }

    [TestMethod]
    public void ErrorViewModelShowsErrorTemplate()
    {
        var mockSessionFactory = new Mock<ISessionFactory>();
        using var appLogger = new TestNoOpApplicationLogger();
        var dialogVM = new AppWideModalErrorDialogViewModel("title", new ErrorControlViewModel(new InvalidOperationException("test"), appLogger, mockSessionFactory.Object, "leading text"));
        var result = AppWideDialogTemplateSelector.Instance.SelectTemplate(dialogVM, this.TestElement!);
        Assert.IsTrue(ReferenceEquals(result, this.ErrorTemplate));
    }
}
