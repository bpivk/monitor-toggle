using System.Drawing;
using System.Windows.Forms;
using RigToggle.Core;

namespace RigToggle.App
{
    /// <summary>
    /// Applies RigToggle.Core.ReleaseNotesFormatter's parsed ReleaseNoteRun sequence
    /// to a RichTextBox via SelectionFont/SelectionBullet (D-01, 26-UI-SPEC.md
    /// Component Specification item 2 and Typography table) -- the App-layer half of
    /// the Core/App parsing split. This class knows nothing about Markdown, only how
    /// to paint already-parsed runs; ReleaseNotesFormatter knows nothing about
    /// WinForms.
    ///
    /// T-26-12 (tampering): only ever assigns SelectionFont/SelectionBullet/
    /// AppendText -- never Rtf or any property that would interpret markup -- so a
    /// release body containing RTF control words or other markup is displayed
    /// literally rather than executed as formatting.
    ///
    /// Deliberately does not set BackColor/ForeColor: ThemeApplier.ThemeRichTextBox
    /// owns both and re-applies them on every theme flip (26-UI-SPEC.md Color
    /// table) -- a renderer that also set them would fight it.
    /// </summary>
    internal static class ReleaseNotesRenderer
    {
        /// <summary>
        /// Clears <paramref name="target"/> and repaints it from
        /// <paramref name="body"/>'s parsed runs: bulleted paragraphs via
        /// SelectionBullet, weight/size via a font derived from the control's own
        /// Font (regular at the control's size for Body, bold at the same size for
        /// Label, bold at 12pt for Heading), and a trailing newline after every run
        /// whose EndsLine is true. Resets SelectionBullet to false and
        /// SelectionStart/SelectionLength to 0 at the end so the control opens
        /// scrolled to the top. Every Font instance created here is disposed via
        /// `using`. Wrapped in a try/catch that falls back to assigning the raw body
        /// text directly, so a rendering failure still shows the user something.
        /// </summary>
        internal static void Render(RichTextBox target, string? body)
        {
            try
            {
                target.Clear();

                foreach (ReleaseNoteRun run in ReleaseNotesFormatter.Format(body))
                {
                    target.SelectionStart = target.TextLength;
                    target.SelectionLength = 0;
                    target.SelectionBullet = run.Bullet;

                    using (Font font = CreateFont(target.Font, run.Style))
                    {
                        target.SelectionFont = font;
                    }

                    target.AppendText(run.Text);

                    if (run.EndsLine)
                    {
                        target.SelectionStart = target.TextLength;
                        target.SelectionLength = 0;
                        target.SelectionBullet = false;
                        target.AppendText(Environment.NewLine);
                    }
                }

                target.SelectionBullet = false;
                target.SelectionStart = 0;
                target.SelectionLength = 0;
            }
            catch
            {
                // A rendering failure must still show the user something -- fall
                // back to the raw, unformatted body text directly.
                try
                {
                    target.Text = body ?? string.Empty;
                }
                catch
                {
                    // Cosmetic-only; nothing further to do if even the fallback fails.
                }
            }
        }

        private static Font CreateFont(Font baseFont, ReleaseNoteStyle style) => style switch
        {
            ReleaseNoteStyle.Heading => new Font(baseFont.FontFamily, 12F, FontStyle.Bold),
            ReleaseNoteStyle.Label => new Font(baseFont, FontStyle.Bold),
            _ => new Font(baseFont, FontStyle.Regular),
        };
    }
}
