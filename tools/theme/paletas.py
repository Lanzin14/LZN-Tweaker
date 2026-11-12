# Gera Themes/Palette.Light.xaml e Themes/Palette.Dark.xaml a partir de um lugar só e confere o contraste (WCAG AA)
# antes de gravar. Uso: python tools/theme/paletas.py   (rode na raiz do repositório)
#
# Os dois temas têm as mesmas chaves: as telas usam {DynamicResource Lzn.*} e o ThemeService troca o dicionário.
#   Claro  — direção da referência: fundo cinza-azulado, superfícies brancas, azul de ação.
#   Escuro — Grafite + Azul: chumbo, grafite e azul elétrico; corporativo e discreto.
import os
import sys

LIGHT = {
    'Background': '#F3F5F9', 'Surface': '#FFFFFF', 'SurfaceHover': '#F7F8FB', 'Raised': '#EEF2F7', 'Active': '#E6EBF2',
    'Line': '#E3E7EE', 'LineSubtle': '#ECEFF4', 'LineStrong': '#CBD2DD', 'ScrollHover': '#AAB3C2', 'ControlStroke': '#7D8798',
    'Text': '#0F1724', 'TextSecondary': '#3D4859', 'TextMuted': '#5E6878',
    'Accent': '#2563EB', 'OnAccent': '#FFFFFF', 'AccentStrong': '#1D4ED8', 'AccentPressed': '#1E40AF', 'AccentHover': '#1D4ED8',
    'AccentText': '#1E40AF', 'AccentSoft': '#EAF1FE', 'AccentStroke': '#BCD0FA',
    'Success': '#13733A', 'SuccessSoft': '#E8F6EC', 'SuccessStroke': '#B9E2C6',
    'Warning': '#B45309', 'WarningSoft': '#FDF4E3', 'WarningStroke': '#F1D49D',
    'Danger': '#C62828', 'DangerSoft': '#FDECEC', 'DangerStroke': '#F3C1C1',
    'Info': '#1D4ED8',
    'Sidebar': '#FFFFFF', 'SidebarHover': '#F3F5F9', 'SidebarActive': '#EEF2FB', 'SidebarStroke': '#E3E7EE', 'SidebarMuted': '#5E6878',
    'Overlay': '#66101828',
    'DiscordBlurple': '#5865F2', 'DiscordSoft': '#EEF0FE', 'DiscordStroke': '#C7CCFA',
}

DARK = {
    'Background': '#111317', 'Surface': '#181B21', 'SurfaceHover': '#1D2027', 'Raised': '#21252D', 'Active': '#272C35',
    'Line': '#262A33', 'LineSubtle': '#1F232A', 'LineStrong': '#3A404C', 'ScrollHover': '#4E5666', 'ControlStroke': '#687184',
    'Text': '#E7E9ED', 'TextSecondary': '#B6BCC7', 'TextMuted': '#8D95A3',
    'Accent': '#2F6BEB', 'OnAccent': '#FFFFFF', 'AccentStrong': '#285FD6', 'AccentPressed': '#2453BD', 'AccentHover': '#6E9DFF',
    'AccentText': '#C3D5FF', 'AccentSoft': '#172238', 'AccentStroke': '#2B4270',
    'Success': '#7FD1A8', 'SuccessSoft': '#14261E', 'SuccessStroke': '#22443A',
    'Warning': '#E5C07B', 'WarningSoft': '#282216', 'WarningStroke': '#4A3D22',
    'Danger': '#E47777', 'DangerSoft': '#2A171A', 'DangerStroke': '#5A2A2E',
    'Info': '#7DB7FA',
    'Sidebar': '#15181D', 'SidebarHover': '#1D2027', 'SidebarActive': '#1B2433', 'SidebarStroke': '#23272F', 'SidebarMuted': '#9AA1AE',
    'Overlay': '#99000000',
    'DiscordBlurple': '#5865F2', 'DiscordSoft': '#1A1D3B', 'DiscordStroke': '#353D8C',
}

# Pincel semântico → cor primitiva. As telas só usam os pincéis.
BRUSHES = [
    ('Lzn.Background', 'Background'), ('Lzn.SurfaceSunken', 'Background'),
    ('Lzn.Surface', 'Surface'), ('Lzn.SurfaceHover', 'SurfaceHover'), ('Lzn.SurfaceRaised', 'Raised'), ('Lzn.SurfaceActive', 'Active'),
    ('Lzn.Stroke', 'Line'), ('Lzn.StrokeSubtle', 'LineSubtle'), ('Lzn.StrokeStrong', 'LineStrong'), ('Lzn.ControlStroke', 'ControlStroke'),
    ('Lzn.Overlay', 'Overlay'),
    ('Lzn.Text', 'Text'), ('Lzn.TextSecondary', 'TextSecondary'), ('Lzn.TextMuted', 'TextMuted'), ('Lzn.TextOnAccent', 'OnAccent'),
    ('Lzn.Accent', 'Accent'), ('Lzn.AccentStrong', 'AccentStrong'), ('Lzn.AccentPressed', 'AccentPressed'), ('Lzn.AccentHover', 'AccentHover'),
    ('Lzn.AccentSoft', 'AccentSoft'), ('Lzn.AccentStroke', 'AccentStroke'), ('Lzn.AccentText', 'AccentText'), ('Lzn.FocusRing', 'AccentHover'),
    ('Lzn.RiskLow', 'Success'), ('Lzn.RiskWarn', 'Warning'), ('Lzn.RiskHigh', 'Danger'), ('Lzn.Info', 'Info'),
    ('Lzn.SuccessSoft', 'SuccessSoft'), ('Lzn.SuccessStroke', 'SuccessStroke'), ('Lzn.WarnSoft', 'WarningSoft'), ('Lzn.WarnStroke', 'WarningStroke'),
    ('Lzn.DangerSoft', 'DangerSoft'), ('Lzn.DangerStroke', 'DangerStroke'),
    ('Lzn.Sidebar', 'Sidebar'), ('Lzn.Sidebar.Hover', 'SidebarHover'), ('Lzn.Sidebar.Active', 'SidebarActive'), ('Lzn.Sidebar.Stroke', 'SidebarStroke'),
    ('Lzn.Sidebar.Text', 'Text'), ('Lzn.Sidebar.TextMuted', 'SidebarMuted'),
    ('Lzn.Discord', 'DiscordBlurple'), ('Lzn.DiscordSoft', 'DiscordSoft'), ('Lzn.DiscordStroke', 'DiscordStroke'),
    ('Lzn.ScrollThumb', 'LineStrong'), ('Lzn.ScrollThumbHover', 'ScrollHover'),
]

# (texto/elemento, fundo, mínimo, uso) — o mesmo conjunto do teste DesignSystemGuardTests.
PAIRS = [
    ('Text', 'Background', 4.5, 'texto sobre o fundo'), ('Text', 'Surface', 4.5, 'texto sobre superfície'),
    ('Text', 'Active', 4.5, 'texto sobre item ativo'), ('TextSecondary', 'Raised', 4.5, 'secundário sobre linha selecionada'),
    ('TextMuted', 'Background', 4.5, 'apagado sobre o fundo'), ('TextMuted', 'Surface', 4.5, 'apagado sobre superfície'),
    ('TextMuted', 'Raised', 4.5, 'apagado sobre linha selecionada'), ('OnAccent', 'Accent', 4.5, 'texto do botão principal'),
    ('OnAccent', 'AccentStrong', 4.5, 'botão principal no hover'), ('AccentHover', 'Surface', 4.5, 'links sobre superfície'),
    ('AccentHover', 'Background', 4.5, 'links sobre o fundo'), ('AccentText', 'AccentSoft', 4.5, 'texto de selo de destaque'),
    ('Success', 'Surface', 4.5, 'estado ativo'), ('Warning', 'Surface', 4.5, 'estado pendente'), ('Danger', 'Surface', 4.5, 'erro'),
    ('Success', 'SuccessSoft', 4.5, 'selo de sucesso'), ('Warning', 'WarningSoft', 4.5, 'selo de atenção'), ('Danger', 'DangerSoft', 4.5, 'selo de erro'),
    ('Info', 'Surface', 4.5, 'em andamento'), ('Text', 'Sidebar', 4.5, 'texto da sidebar'), ('SidebarMuted', 'Sidebar', 4.5, 'secundário da sidebar'),
    ('Text', 'SidebarActive', 4.5, 'item ativo da sidebar'), ('Warning', 'Sidebar', 4.5, 'aviso no rodapé da sidebar'),
    ('ControlStroke', 'Background', 3.0, 'borda de controle sobre o fundo'), ('ControlStroke', 'Surface', 3.0, 'borda de controle sobre superfície'),
    ('ControlStroke', 'Raised', 3.0, 'borda de controle sobre linha selecionada'), ('Accent', 'Raised', 3.0, 'interruptor ligado sobre linha selecionada'),
    ('Accent', 'Surface', 3.0, 'indicador de seleção'),
]


def luminance(hex_color):
    h = hex_color.lstrip('#')[-6:]
    channels = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    channels = [c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4 for c in channels]
    return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2]


def contrast(a, b):
    la, lb = luminance(a), luminance(b)
    return (max(la, lb) + 0.05) / (min(la, lb) + 0.05)


def check(name, palette):
    failures = []
    for fg, bg, minimum, usage in PAIRS:
        ratio = contrast(palette[fg], palette[bg])
        if ratio < minimum:
            failures.append(f'{name}: {usage} ({fg} sobre {bg}) = {ratio:.2f}:1, mínimo {minimum}:1')
    return failures


def write(path, title, description, palette):
    assert set(palette) == set(LIGHT), 'os dois temas precisam das mesmas cores'
    lines = [
        '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
        f'    <!-- {title}: {description}',
        '         Gerado por tools/theme/paletas.py (não edite à mão: mude lá e rode de novo). Único lugar com hexadecimais,',
        '         junto com o outro tema; as telas usam {DynamicResource Lzn.*} e o ThemeService troca este dicionário. -->',
        '',
        '    <!-- Cores primitivas -->',
    ]
    lines += [f'    <Color x:Key="Lzn.Color.{key}">{value}</Color>' for key, value in palette.items()]
    lines += ['', '    <!-- Pincéis semânticos (as telas usam estes) -->']
    lines += [f'    <SolidColorBrush x:Key="{brush}" Color="{{StaticResource Lzn.Color.{color}}}" />' for brush, color in BRUSHES]
    lines += ['</ResourceDictionary>', '']
    with open(path, 'w', encoding='utf-8', newline='\r\n') as file:
        file.write('\n'.join(lines))


if __name__ == '__main__':
    problems = check('Claro', LIGHT) + check('Escuro', DARK)
    for name, palette in (('Claro', LIGHT), ('Escuro', DARK)):
        print(f'{name}: ' + ', '.join(f'{u} {contrast(palette[f], palette[b]):.1f}' for f, b, _, u in PAIRS[:4]))
    if problems:
        print('\n'.join(problems))
        sys.exit(1)
    themes = os.path.join('src', 'LznTweaker.App', 'Themes')
    write(os.path.join(themes, 'Palette.Light.xaml'), 'Tema claro', 'fundo cinza-azulado, superfícies brancas e azul de ação.', LIGHT)
    write(os.path.join(themes, 'Palette.Dark.xaml'), 'Tema escuro Grafite + Azul', 'chumbo, grafite e azul elétrico.', DARK)
    print('Paletas gravadas; todos os pares de contraste passam.')
