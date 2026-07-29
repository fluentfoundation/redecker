import { defineConfig, type HeadConfig } from 'vitepress'

// GitHub Pages serves this as a project site under /redecker/, not at the domain root, so every
// asset and internal link has to be prefixed. Overridable for a custom domain, where the site
// would sit at '/' instead.
const base = process.env.DOCS_BASE ?? '/redecker/'

// VitePress rewrites links inside markdown and themeConfig, but head entries are passed through
// verbatim -- so these have to carry the prefix themselves or the favicon 404s.
const asset = (path: string) => base.replace(/\/$/, '') + path

const head: HeadConfig[] = [
    ['link', { rel: 'icon', href: asset('/redecker-icon.svg') }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'Redecker' }],
    ['meta', {
        property: 'og:description',
        content: 'An update tool for .NET dependencies that reads packages, not just their version graph.'
    }],
    // Social previews are fetched by crawlers with no page context, so this one must be absolute.
    ['meta', { property: 'og:image', content: 'https://fluentfoundation.github.io/redecker/redecker-icon-512.png' }],
]

// https://vitepress.dev/reference/site-config
export default defineConfig({
    title: 'Redecker',
    description: 'An update tool for .NET dependencies that reads packages, not just their version graph',
    lang: 'en-US',
    head,
    base,
    cleanUrls: true,

    // A broken link in the docs should fail the build rather than ship: this site is largely
    // about the cost of warnings nobody reads.
    ignoreDeadLinks: false,

    themeConfig: {
        outline: 2,
        logo: '/redecker-icon.svg',
        externalLinkIcon: true,

        nav: [
            { text: 'Problems', link: '/problems' },
            { text: 'Comparison', link: '/comparison' },
            { text: 'Evidence', link: '/evidence' },
            { text: 'Guide', link: '/guide/getting-started' },
            { text: 'Rules', link: '/rules/' },
            { text: 'Concepts', link: '/concepts/pin-hints' },
            { text: 'Releases', link: 'https://github.com/fluentfoundation/redecker/releases' },
        ],

        sidebar: {
            '/': [
                {
                    text: 'Start here',
                    items: [
                        { text: 'The problems', link: '/problems' },
                        { text: 'Real-world examples', link: '/evidence' },
                        { text: 'Getting Started', link: '/guide/getting-started' },
                        { text: 'Comparison', link: '/comparison' },
                    ]
                },
                {
                    text: 'Guide',
                    items: [
                        { text: 'Commands', link: '/guide/commands' },
                        { text: 'MSBuild Integration', link: '/guide/msbuild' },
                    ]
                },
                {
                    text: 'Rules',
                    items: [
                        { text: 'Overview', link: '/rules/' },
                        { text: 'RDK0001 Dangling assets', link: '/rules/rdk0001' },
                        { text: 'RDK0002 Asset loss', link: '/rules/rdk0002' },
                        { text: 'RDK0003 Split lockstep family', link: '/rules/rdk0003' },
                        { text: 'RDK0004 Undocumented transitive pin', link: '/rules/rdk0004' },
                        { text: 'RDK0005 Tool package not installable', link: '/rules/rdk0005' },
                        { text: 'RDK0007 Untracked output copies', link: '/rules/rdk0007' },
                    ]
                },
                {
                    text: 'Concepts',
                    items: [
                        { text: 'Pin Hints', link: '/concepts/pin-hints' },
                        { text: 'Framework Bands', link: '/concepts/framework-bands' },
                        { text: 'Epochs (not supported)', link: '/concepts/epochs' },
                    ]
                },
            ]
        },

        socialLinks: [
            { icon: 'github', link: 'https://github.com/fluentfoundation/redecker' },
        ],

        footer: {
            message: 'Released under the MIT License.',
            copyright: 'Copyright © 2026 Fluent Foundation'
        },

        search: {
            provider: 'local'
        },

        editLink: {
            pattern: 'https://github.com/fluentfoundation/redecker/edit/main/docs-website/:path',
            text: 'Edit this page on GitHub',
        },

        lastUpdated: {
            text: 'Updated at',
            formatOptions: {
                dateStyle: 'full',
                timeStyle: 'medium',
            },
        },
    },
})
