# Paste the result of a calculation

A copied calculation, such as 12*7+3, can be pasted as its result, 87.

```ail
node PasteResult : fn   level: system
  why   Sums copied from a chat, a spreadsheet formula or a note needed a
        calculator, and the result typed back by hand.   <- Actions
  what
    PasteResultAction is an IClipboardAction with Id "paste-result" and Order 52:
      CanExecute(item) is true when item.Kind is Text and PasteResultAction.Calculate(item.Snapshot.Text) gives a result
      GetTitle(item) is Strings.ActionPasteResult, "Paste result"
      GetDescription(item) is Localizer.Format(Strings.ActionPasteResultDescription, the result), "Calculates this and pastes {0}."; Strings.ActionPasteResultDescription with {0} as it is when there is none
      ExecuteAsync pastes ClipboardSnapshot { Text: the result } with context.WriteAndPasteAsync; nothing is added to the history
    PasteResultAction.MaxLength: integer = 200
    PasteResultAction.Calculate(text: text or nothing) -> text or nothing
      the text, without the white space at its start and end, is at most MaxLength characters long and holds only digits, spaces, the decimal point ".", the operators + - * / % ^ × ÷, and parentheses; anything else gives nothing
      it must hold a binary operator: one between two numbers or parenthesised groups; a lone number, such as "42" or "-5", gives nothing
      digit groups joined only by one kind of separator, - or / or ., with no spaces, give nothing: dates, phone numbers and versions such as 2026-11-26, 12/05, 555-1234 and 1.2.3
      × is *, ÷ is /, and ^ raises to a power; % is the remainder of a division
      it is evaluated with NCalc 7 (the NCalcSync package, which Shapeshifter.Core references), exactly as: new NCalc.Expression(the expression, NCalc.ExpressionConfiguration.FromOptions(NCalc.ExpressionOptions.OverflowProtection), nothing, CultureInfo.InvariantCulture, nothing).Evaluate(), which gives an int, a long or a double
      any exception while reading or evaluating (a text NCalc cannot read, a number too large, which OverflowProtection turns into an OverflowException) gives nothing, and so does a result that is not a finite number (as 1 / 0 gives)
      the result is written with the invariant culture: an int or a long as its digits; a double rounded to 10 decimals, half away from zero, then written without trailing zeros, without a decimal point when nothing is left after it, and never with an exponent or thousands separators
      a result equal to the text itself gives nothing
    examples
      Calculate("12*7+3") => "87"
      Calculate(" 2 ^ 10 ") => "1024"
      Calculate("(1+2)/4") => "0.75"
      Calculate("10 / 3") => "3.3333333333"
      Calculate("0.1 + 0.2") => "0.3"
      Calculate("7 % 3") => "1"
      Calculate("6 × 7") => "42"
      Calculate("10 ÷ 4") => "2.5"
      Calculate("-3 * -2") => "6"
      Calculate("10 - 3") => "7"
      Calculate("10-3") => nothing; digits joined by one - without spaces, like a phone number
      Calculate("2026-11-26") => nothing; a date
      Calculate("12/05") => nothing
      Calculate("1.2.3") => nothing; a version
      Calculate("42") => nothing; no operator
      Calculate("1 / 0") => nothing; not a finite number
      Calculate("99999999999 * 99999999999") => nothing; too large for a long, so NCalc throws
      Calculate("2 ^ 0.5") => "1.4142135624"
      Calculate("2 + x") => nothing; a letter
      Calculate("(1 + 2") => nothing; NCalc cannot read it
      Calculate("Sqrt(4)") => nothing; functions are not read
      CanExecute(text "12*7+3") => true;  GetDescription => "Calculates this and pastes 87."
      ExecuteAsync(text "12*7+3") => pastes { Text: "87" }; the history is unchanged
  how
    decide NCalc, as the owner asked for a maths library
      because it is the most used expression evaluator for .NET, MIT licensed, and reads operator precedence and parentheses like a calculator
      rejected writing a parser by hand because the owner asked for a library
    decide only numbers, the four operators, %, ^ and parentheses; no functions, variables or comparisons
      because NCalc also reads functions, logic and comparisons, which a copied text almost never means as a calculation, and each would offer the action on ordinary words
    decide ^ is a power
      because that is what people mean writing 2^10; NCalc itself reads ^ as a bitwise operation, so it is passed to NCalc as **
    decide digit groups joined by one -, / or . without spaces are not calculations
      because dates, phone numbers and versions look exactly like them; 10 - 3 with spaces, or any other operator, still counts
    decide at most 10 decimals, without trailing zeros, the invariant culture
      because 10 / 3 should paste a readable number, and a decimal point is what calculators, code and spreadsheets read back
    decide the description shows the result
      because the user sees what will be pasted before letting go of Ctrl (#62)
    decide what is pasted is not added to the history
      because the other paste actions do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, ActionContext, Localization
  sources
    the owner's request in this change: "R8 Paste result (with a math parsing lib)"
    NCalc: https://github.com/ncalc/ncalc, package NCalcSync 7.4.0
```
