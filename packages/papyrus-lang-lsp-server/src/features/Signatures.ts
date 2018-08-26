import {
    DisplayText,
    DisplayTextEmitter,
} from 'papyrus-lang/lib/program/DisplayTextEmitter';
import { FunctionSymbol } from 'papyrus-lang/lib/symbols/Symbol';
import {
    ParameterInformation,
    SignatureInformation,
} from 'vscode-languageserver';

function parameterInformationForFunctionSignature(
    displayText: DisplayText
): ParameterInformation {
    return {
        label: displayText.text,
    };
}
export function signatureInformationForFunctionSymbol(
    symbol: FunctionSymbol,
    displayTextEmitter: DisplayTextEmitter
): SignatureInformation {
    const displayText = displayTextEmitter.getDisplayTextForFunction(symbol);

    return {
        documentation: displayText.documentation,
        label: `${displayText.shortNamePrefix}(${displayText.parameters
            .map((p) => p.text)
            .join(', ')})`,
        parameters: displayText.parameters.map(
            parameterInformationForFunctionSignature
        ),
    };
}
